using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BookKeepingWeb.Helpers
{
    public class PaginatedList<T> : List<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            this.AddRange(items);
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        /// <summary>
        /// Create a paginated list asynchronously
        /// </summary>
        /// <param name="source">source list</param>
        /// <param name="pageIndex">the page</param>
        /// <param name="pageSize">the number of posts</param>
        /// <returns>a paginated list of posts</returns>
        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
        {
            // Defensive fix: Prevent negative or zero page numbers
            if (pageIndex < 1) pageIndex = 1;

            var count = await source.CountAsync();
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }

        /// <summary>
        /// Create a paginated list synchronously
        /// </summary>
        /// <param name="source">source list</param>
        /// <param name="pageIndex">the page</param>
        /// <param name="pageSize">the number of posts</param>
        /// <returns>a paginated list of posts</returns>
        public static PaginatedList<T> Create(IQueryable<T> source, int pageIndex, int pageSize)
        {
            // Defensive fix: Prevent negative or zero page numbers
            if (pageIndex < 1) pageIndex = 1;

            var count = source.Count();
            var items = source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}
