using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;

namespace FileUploadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public FileUploadController(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;
        }

        [HttpPost("upload")]
        public IActionResult UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file selected.");

            // ensure uploads folder exists
            string uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // generate unique name
            string fileName = $"{Guid.NewGuid()}_{file.FileName}";
            string filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            string relativePath = $"/uploads/{fileName}";
            string fileType = Path.GetExtension(file.FileName);

            // 🔑 Insert into DB using stored procedure
            using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand("InsertUploadedFile", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@OriginalName", file.FileName);
                    cmd.Parameters.AddWithValue("@StoredName", fileName);
                    cmd.Parameters.AddWithValue("@FilePath", relativePath);
                    cmd.Parameters.AddWithValue("@FileType", fileType);

                    con.Open();
                    cmd.ExecuteNonQuery();
                    con.Close();
                }
            }

            string fileUrl = $"{Request.Scheme}://{Request.Host}{relativePath}";
            return Ok(new
            {
                OriginalName = file.FileName,
                StoredName = fileName,
                FileUrl = fileUrl
            });
        }
    }
}
