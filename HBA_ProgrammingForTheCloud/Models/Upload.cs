﻿using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace HBA_ProgrammingForTheCloud.Models
{
    [FirestoreData]
    public class Upload
    {
        [FirestoreProperty]
        [Required]
        public string VideoName { get; set; }
        [FirestoreProperty]
        public Timestamp UploadDate { get; set; }
        public DateTime DTUpload
        {
            get { return UploadDate.ToDateTime(); }
            set { UploadDate = Google.Cloud.Firestore.Timestamp.FromDateTime(value.ToUniversalTime()); }
        }
        [FirestoreProperty]
        public string Username { get; set; }
        [FirestoreProperty]
        public string BucketId { get; set; }
        [FirestoreProperty]
        public string ThumbnailUrl { get; set; }
        [FirestoreProperty]
        public bool Active { get; set; }
    }
}