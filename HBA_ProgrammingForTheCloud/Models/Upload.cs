using Google.Cloud.Firestore;
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
        [Required]
        public DateTime UploadDate { get; set; }
        [FirestoreProperty]
        [Required]
        public string UserId { get; set; }
        [FirestoreProperty]
        [Required]
        public string BucketId { get; set; }
        [FirestoreProperty]
        [Required]
        public string ThumbnailUrl { get; set; }
        [FirestoreProperty]
        [Required]
        public bool Active { get; set; }
    }
}
