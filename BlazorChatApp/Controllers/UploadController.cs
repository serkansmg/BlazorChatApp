using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using BlazorChatApp.Models.Chat;

namespace BlazorChatApp.Controllers
{
    [DisableRequestSizeLimit]
    public class UploadController : Controller
    {
        private readonly IWebHostEnvironment environment;

        public UploadController(IWebHostEnvironment environment)
        {
            this.environment = environment;
        }

        [HttpPost("upload/media")]
        public IActionResult Media(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                // Dosya türünü kontrol et
                var messageType = GetMessageTypeFromFile(file);
                
                // Güvenli dosya adı oluştur
                var fileName = $"{messageType.ToString().ToLower()}-{DateTime.Today:yyyy-MM-dd}-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var uploadsPath = Path.Combine(environment.WebRootPath, "uploads");
                
                // uploads klasörü yoksa oluştur
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                // URL döndür
                var url = Url.Content($"~/uploads/{fileName}");

                return Ok(new { 
                    Url = url,
                    FileName = file.FileName,
                    FileSize = file.Length,
                    MimeType = file.ContentType,
                    MessageType = messageType.ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private MessageType GetMessageTypeFromFile(IFormFile file)
        {
            var contentType = file.ContentType.ToLower();
            return contentType switch
            {
                var ct when ct.StartsWith("image/") => MessageType.Image,
                var ct when ct.StartsWith("video/") => MessageType.Video,
                var ct when ct.StartsWith("audio/") => MessageType.Audio,
                _ => MessageType.File
            };
        }
    }
}