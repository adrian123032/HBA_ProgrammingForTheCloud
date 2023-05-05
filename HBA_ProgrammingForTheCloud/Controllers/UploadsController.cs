using Common.DataAccess;
using Common.Models;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using HBA_ProgrammingForTheCloud.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HBA_ProgrammingForTheCloud.Controllers
{
    [Authorize]
    public class UploadsController : Controller
    {
        FirestoreUploadRepository _uploadsRepo;
        ILogger<UploadsController> _logger;
        PubSubTranscriptRepository _psRepository;
        public UploadsController(FirestoreUploadRepository uploadsRepo, ILogger<UploadsController> logger, PubSubTranscriptRepository psRepository)
        {
            _uploadsRepo = uploadsRepo;
            _logger = logger;
            _psRepository = psRepository;
        }
        public async Task<IActionResult> Index()
        {
            var list = await _uploadsRepo.GetUploads(User.Identity.Name);
            return View(list);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Create(Upload up, IFormFile file, IFormFile thumbnail, [FromServices] IConfiguration config)
        {
            up.UploadDate = Timestamp.FromDateTime(DateTime.Now.ToUniversalTime());
            up.Username = User.Identity.Name;
            up.Transcribed = false;
            up.Transcription = "";

            byte[] bytes;
            string fileExtension = ""; 
            if (file != null)
            {
                fileExtension = Path.GetExtension(file.FileName);
            }

            string thumbnailExtension = "";
            if (thumbnail != null)
            {
                thumbnailExtension = Path.GetExtension(thumbnail.FileName);
            }

            _logger.LogInformation($"User {User.Identity.Name} is creating an upload named {up.VideoName}");
            if ((ModelState.IsValid) && (fileExtension.ToLower() == ".mp4")) { 
                if ((thumbnailExtension.ToLower() == ".jpg") || (thumbnailExtension.ToLower() == ".png") || (thumbnailExtension.ToLower() == ".jpeg")) {
                    _logger.LogInformation($"Validators for {up.VideoName} are ok");
                    using (MemoryStream ms = new MemoryStream())
                    {
                        thumbnail.CopyTo(ms);
                        bytes = ms.ToArray();
                        
                    }
                    up.ThumbnailString = Convert.ToBase64String(bytes);
                    try
                    {
                        string bucketName = config["bucket"].ToString();
                        if (file != null)
                        {
                            var storage = StorageClient.Create();
                            using var fileStream = file.OpenReadStream();

                            string newFileName = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(file.FileName);
                            storage.UploadObject(bucketName, newFileName, null, fileStream);

                            up.BucketId = $"https://storage.googleapis.com/{bucketName}/{newFileName}";
                        }
                        _uploadsRepo.AddUpload(up);
                        TempData["success"] = "Upload was added sucessfully";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"{User.Identity.Name} had an error while uploading a file");
                        TempData["error"] = "Upload encountered an issue!";
                    }
                }
                else
                {
                    TempData["error"] = "Thumbnail type is invalid! Upload jpg or png";
                }

            }
            else
            {
                TempData["error"] = "File type is invalid! Upload mp4";
            }

            return View();
        }

        public async Task<IActionResult> Delete(string BucketId)
        {
            try
            {
                await _uploadsRepo.Delete(BucketId);
                TempData["success"] = "Upload was deleted successfully from the database";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{User.Identity.Name} had an error while deleting an upload");
                TempData["error"] = "Upload could not be deleted";

                //logging ex in google cloud (Error reporting)
            }
            return RedirectToAction("Index");
        }



        public async Task<IActionResult> Transcribe(string bucketId)
        {
            Upload up = await _uploadsRepo.GetUpload(bucketId);
            await _psRepository.PushMessage(up);
            try
            {
                TempData["success"] = "Transcribe is queued!";
            }
            catch (Exception ex)

            {
                _logger.LogError(ex, $"{User.Identity.Name} had an error while updating an upload");
                TempData["error"] = "Could not transcribe!";
            }
            return RedirectToAction("Index");
        }
    }
}
