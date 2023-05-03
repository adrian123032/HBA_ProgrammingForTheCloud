using Google.Cloud.Firestore;
using HBA_ProgrammingForTheCloud.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HBA_ProgrammingForTheCloud.DataAccess
{
    public class FirestoreUploadRepository
    {
        FirestoreDb db;
        public FirestoreUploadRepository(string project)
        {
            db = FirestoreDb.Create(project);
        }

        public async void AddUpload(Upload up)
        {
            await db.Collection("uploads").Document().SetAsync(up);
        }

        public async Task<List<Upload>> GetUploads()
        {
            List<Upload> uploads = new List<Upload>();
            Query allUploadsQuery = db.Collection("uploads");
            QuerySnapshot allUploadsQuerySnapshot = await allUploadsQuery.GetSnapshotAsync();
            foreach(DocumentSnapshot documentSnapshot in allUploadsQuerySnapshot.Documents)
            {
                Upload up = documentSnapshot.ConvertTo<Upload>();
                uploads.Add(up);
            }

            return uploads;
        }
    }
}
