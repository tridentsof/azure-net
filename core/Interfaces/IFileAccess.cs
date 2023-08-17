using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace core.Interfaces
{
    public interface IFileAccess
    {
        string CreateContainer(string bucketName);
        string DeleteContainer(string bucketName);
        string GetSASUrl(string bucketName, string fileName, int expiredInDays);
        bool CheckExistsContainer(string bucketName);
        byte[] ReadingAnObject(string bucketName, string fileName);
        byte[] ReadingAnObjectMultiPart(string bucketName, string fileName, string filePath);
        bool WritingAnObject(string bucketName, string fileName, byte[] fileBytes, int size);
        bool RenameFile(string bucketName, string oldKeyFile, string newKeyFile);
        bool UploadFile(string bucketName, string fileName, string filePath);
        bool DeletingAnObject(string bucketName, string fileName);
        bool DeleteFileTemp(string bucketName, DateTime currentDate, ref string error);
    }
}
