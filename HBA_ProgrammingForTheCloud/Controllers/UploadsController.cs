using HBA_ProgrammingForTheCloud.DataAccess;
using HBA_ProgrammingForTheCloud.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public UploadsController(FirestoreUploadRepository uploadsRepo)
        {
            _uploadsRepo = uploadsRepo;
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
            if (ModelState.IsValid) { 
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
