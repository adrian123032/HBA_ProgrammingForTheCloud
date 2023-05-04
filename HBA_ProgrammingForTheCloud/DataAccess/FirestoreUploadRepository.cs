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

        public async Task<List<Upload>> GetUploads(string username)
        {
            List<Upload> uploads = new List<Upload>();
            Query allUploadsQuery = db.Collection("uploads");
            QuerySnapshot allUploadsQuerySnapshot = await allUploadsQuery.GetSnapshotAsync();
            foreach(DocumentSnapshot documentSnapshot in allUploadsQuerySnapshot.Documents)
            {
                Upload up = documentSnapshot.ConvertTo<Upload>();
                if (up.Username == username)
                {
                    uploads.Add(up);
                }
            }

            return uploads;
        }

        public async void Update(Upload up)
        {
            Query booksQuery = db.Collection("uploads").WhereEqualTo("BucketId", up.BucketId);
            QuerySnapshot booksQuerySnapshot = await booksQuery.GetSnapshotAsync();

            DocumentSnapshot documentSnapshot = booksQuerySnapshot.Documents.FirstOrDefault();
            if (documentSnapshot.Exists == false) throw new Exception("Upload does not exist");
            else
            {
                DocumentReference booksRef = db.Collection("uploads").Document(documentSnapshot.Id);
                await booksRef.SetAsync(up);
            }
        }

        public async Task<Upload> GetUpload(string bucketId)
        {
            Query booksQuery = db.Collection("uploads").WhereEqualTo("BucketId", bucketId);
            QuerySnapshot booksQuerySnapshot = await booksQuery.GetSnapshotAsync();

            DocumentSnapshot documentSnapshot = booksQuerySnapshot.Documents.FirstOrDefault();
            if (documentSnapshot.Exists == false) return null;
            else
            {
                Upload result = documentSnapshot.ConvertTo<Upload>();
                return result;
            }
        }

        public async Task Delete(string bucketId)
        {

            Query booksQuery = db.Collection("uploads").WhereEqualTo("BucketId", bucketId);
            QuerySnapshot booksQuerySnapshot = await booksQuery.GetSnapshotAsync();

            DocumentSnapshot documentSnapshot = booksQuerySnapshot.Documents.FirstOrDefault();
            if (documentSnapshot.Exists == false) throw new Exception("Upload does not exist");
            else
            {
                DocumentReference booksRef = db.Collection("uploads").Document(documentSnapshot.Id);
                await booksRef.DeleteAsync();
            }
        }
    }
}
