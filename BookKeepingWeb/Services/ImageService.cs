
using BookKeepingWeb.Data;
using BookKeepingWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace BookKeepingWeb.Services
{
    public struct Content
    {
        public string? path;
        public FileType fileType;
    }

    public class ImageService
    {
        private readonly ApplicationDbContext _context;
        public ImageService(ApplicationDbContext db) 
        {
            _context = db;
        }

        public async Task<IEnumerable<string?>> GetImages(int page, int pageSize)
        {
            return await _context.UploadContents
                .OrderByDescending(c => c.CreatedDateTime)
                .Where(i => i.ContentPath != null)
                .Select(i => i.ContentPath)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
