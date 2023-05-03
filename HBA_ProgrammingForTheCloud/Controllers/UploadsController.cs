using Google.Cloud.Firestore;
using HBA_ProgrammingForTheCloud.DataAccess;
using HBA_ProgrammingForTheCloud.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HBA_ProgrammingForTheCloud.Controllers
{
    [Authorize]
    public class UploadsController : Controller
    {
        FirestoreUploadRepository _uploadsRepo;
        ILogger<UploadsController> _logger;
        public UploadsController(FirestoreUploadRepository uploadsRepo, ILogger<UploadsController> logger)
        {
            _uploadsRepo = uploadsRepo;
            _logger = logger;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Create(Upload up)
        {
            up.UploadDate = Timestamp.FromDateTime(DateTime.Now.ToUniversalTime());
            up.Username = User.Identity.Name;
            up.BucketId = "123";
            up.ThumbnailUrl = "123";
            up.Active = true;
            _logger.LogInformation($"User {User.Identity.Name} is creating an upload named {up.VideoName}");
            if (ModelState.IsValid) {
                _logger.LogInformation($"Validators for {up.VideoName} are ok");
                try
                { 
                    _uploadsRepo.AddUpload(up);
                    TempData["success"] = "Upload was added sucessfully";
                }
                catch (Exception e)
                {
                    TempData["error"] = "Upload encountered an issue!";
                }
            }

            return View();
        }
    }
}
