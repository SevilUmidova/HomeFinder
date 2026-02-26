using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder.Controllers
{
    public class HomeController : Controller
    {
        private readonly HomeFinderContext _context;

        public HomeController(HomeFinderContext context)
        {
            _context = context;
        }

        public IActionResult Index(
            decimal? priceMin,
            decimal? priceMax,
            int? sizeMin,
            int? sizeMax,
            int? rooms,
            string city = "",
            string district = "",
            string address = "",
            string sortBy = "rating",
            string alltext = "")
        {
            var query = _context.Apartments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .Include(a => a.ReviewApartments)
                .AsQueryable();

            if (priceMin.HasValue) query = query.Where(a => a.Price >= priceMin);
            if (priceMax.HasValue) query = query.Where(a => a.Price <= priceMax);
            if (sizeMin.HasValue) query = query.Where(a => a.Size >= sizeMin);
            if (sizeMax.HasValue) query = query.Where(a => a.Size <= sizeMax);
            if (rooms.HasValue) query = query.Where(a => a.Rooms >= rooms);

            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(a => a.Addresses.Any(ad => ad.City != null && ad.City.Contains(city)));

            if (!string.IsNullOrWhiteSpace(district))
                query = query.Where(a => a.Addresses.Any(ad => ad.District != null && ad.District.Contains(district)));

            if (!string.IsNullOrWhiteSpace(address))
                query = query.Where(a => a.Addresses.Any(ad => ad.StreetAddress != null && ad.StreetAddress.Contains(address)));

            if (!string.IsNullOrWhiteSpace(alltext))
            {
                var text = alltext.Trim();

                query = query.Where(a =>
                    (a.Description != null && a.Description.Contains(text)) ||
                    a.Addresses.Any(ad =>
                        (ad.City != null && ad.City.Contains(text)) ||
                        (ad.District != null && ad.District.Contains(text)) ||
                        (ad.StreetAddress != null && ad.StreetAddress.Contains(text)) ||
                        (ad.BuildingNumber != null && ad.BuildingNumber.Contains(text))) ||
                    (a.User != null &&
                        ((a.User.FirstName != null && a.User.FirstName.Contains(text)) ||
                         (a.User.LastName != null && a.User.LastName.Contains(text))))
                );
            }

            query = sortBy switch
            {
                "rating_asc" => query.OrderBy(a => a.ReviewApartments.Any()
                    ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0))
                    : 0),

                "price_asc" => query.OrderBy(a => a.Price),
                "price_desc" => query.OrderByDescending(a => a.Price),
                "newest" => query.OrderByDescending(a => a.ApartmentId),
                "reviews" => query.OrderByDescending(a => a.ReviewApartments.Count),

                _ => query.OrderByDescending(a => a.ReviewApartments.Any()
                    ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0))
                    : 0)
            };

            var viewModels = query
                .Select(a => new ApartmentViewModel
                {
                    ApartmentId = a.ApartmentId,
                    Description = a.Description,
                    Price = a.Price ?? 0,
                    Size = a.Size ?? 0,
                    Rooms = a.Rooms ?? 0,

                    StreetAddress = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.StreetAddress)
                        .FirstOrDefault(),

                    BuildingNumber = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.BuildingNumber)
                        .FirstOrDefault(),

                    District = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.District)
                        .FirstOrDefault(),

                    City = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.City)
                        .FirstOrDefault(),

                    Latitude = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.Latitude)
                        .FirstOrDefault(),

                    Longitude = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.Longitude)
                        .FirstOrDefault(),

                    PhotoPaths = a.Photos
                        .OrderBy(p => p.PhotoId)
                        .Select(p => p.PhotoPath)
                        .ToList(),

                    LandlordName = a.User != null ? (a.User.FirstName + " " + a.User.LastName) : null,
                    PhoneNumber = a.User != null ? a.User.PhoneNumber : null,

                    AverageRating = a.ReviewApartments.Any()
                        ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0))
                        : 0,

                    ReviewCount = a.ReviewApartments.Count
                })
                .Take(200)
                .ToList();

            ViewData["SortBy"] = sortBy;
            ViewData["PriceMin"] = priceMin;
            ViewData["PriceMax"] = priceMax;
            ViewData["SizeMin"] = sizeMin;
            ViewData["SizeMax"] = sizeMax;
            ViewData["Rooms"] = rooms;
            ViewData["City"] = city;
            ViewData["District"] = district;
            ViewData["Address"] = address;
            ViewData["Alltext"] = alltext;

            return View(viewModels);
        }
    }
}
