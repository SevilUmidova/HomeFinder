using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder.Controllers
{
    public class ApartmentsController : Controller
    {
        private readonly HomeFinderContext _context;
        private readonly IWebHostEnvironment _env;

        public ApartmentsController(HomeFinderContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Проверка авторизации владельца
        private bool IsLandlordLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Landlord";
        }

        // Мои квартиры
        public async Task<IActionResult> MyApartments()
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var apartments = _context.Apartments
                .Where(a => a.UserId == userId)
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .ToList();

            var viewModels = apartments.Select(a => new ApartmentViewModel
            {
                ApartmentId = a.ApartmentId,
                UserId = a.UserId,
                Description = a.Description,
                Price = a.Price ?? 0,
                Size = a.Size ?? 0,
                Rooms = a.Rooms ?? 0,
                StreetAddress = a.Addresses?.FirstOrDefault()?.StreetAddress,
                BuildingNumber = a.Addresses?.FirstOrDefault()?.BuildingNumber,
                ApartmentNumber = a.Addresses?.FirstOrDefault()?.ApartmentNumber,
                District = a.Addresses?.FirstOrDefault()?.District,
                City = a.Addresses?.FirstOrDefault()?.City,
                Region = a.Addresses?.FirstOrDefault()?.Region,
                Latitude = a.Addresses?.FirstOrDefault()?.Latitude,        // ✅ Координаты
                Longitude = a.Addresses?.FirstOrDefault()?.Longitude,     // ✅ Координаты
                PhotoPaths = a.Photos?.Select(p => p.PhotoPath).ToList() ?? new(),
                AverageRating = a.ReviewApartments.Any() ? a.ReviewApartments.Average(r => r.Rating ?? 0) : 0,
                ReviewCount = a.ReviewApartments.Count
            }).ToList();
            var canAdd = await CanAddApartment(userId);
            ViewBag.CanAddApartment = canAdd;

            return View(viewModels);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .Include(a => a.ReviewApartments)
                    .ThenInclude(r => r.User)
                .FirstOrDefault(a => a.ApartmentId == id);

            if (apartment == null)
                return NotFound();

            apartment.Views ??= 0;
            apartment.Views++;
            _context.ApartmentViewLogs.Add(new ApartmentViewLog
            {
                ApartmentId = apartment.ApartmentId,
                ViewedAt = DateTime.Now
            });
            _context.SaveChanges();

            var address = apartment.Addresses.FirstOrDefault();

            var model = new ApartmentViewModel
            {
                ApartmentId = apartment.ApartmentId,
                Description = apartment.Description,
                Price = apartment.Price ?? 0,
                Size = apartment.Size ?? 0,
                Rooms = apartment.Rooms ?? 0,
                Views = apartment.Views ?? 0,

                StreetAddress = address?.StreetAddress,
                BuildingNumber = address?.BuildingNumber,
                ApartmentNumber = address?.ApartmentNumber,
                District = address?.District,
                City = address?.City,
                Region = address?.Region,

                Latitude = address?.Latitude,
                Longitude = address?.Longitude,

                PhotoPaths = apartment.Photos
                    .Select(p => p.PhotoPath)
                    .ToList(),

                LandlordName = apartment.User != null
                    ? apartment.User.FirstName + " " + apartment.User.LastName
                    : "Unknown",

                PhoneNumber = apartment.User?.PhoneNumber,

                AverageRating = apartment.ReviewApartments.Any()
                    ? apartment.ReviewApartments.Average(r => r.Rating ?? 0)
                    : 0,

                ReviewCount = apartment.ReviewApartments.Count,

                Reviews = apartment.ReviewApartments.ToList()
            };

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            if (!await CanAddApartment(userId))
                return RedirectToAction("Premium", "Payment");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApartmentViewModel model)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var apartmentsCount = await _context.Apartments
                .CountAsync(a => a.UserId == userId);

            var subscription = await _context.LandlordSubscriptions
                .FirstOrDefaultAsync(x => x.UserId == userId);

            bool isPremium =
                subscription != null &&
                subscription.Status == "active" &&
                (subscription.CurrentPeriodEndUtc == null ||
                 subscription.CurrentPeriodEndUtc > DateTime.UtcNow);

            if (!isPremium && apartmentsCount >= 1)
                return RedirectToAction("Premium", "Payment");

            var apartment = new Apartment
            {
                UserId = userId,
                Description = model.Description,
                Price = model.Price,
                Size = model.Size,
                Rooms = model.Rooms,
                Photos = new List<Photo>(),
                Addresses = new List<Address>
        {
            new Address
            {
                StreetAddress = model.StreetAddress,
                BuildingNumber = model.BuildingNumber,
                ApartmentNumber = model.ApartmentNumber,
                District = model.District,
                City = model.City,
                Region = model.Region,
                Latitude = model.Latitude,
                Longitude = model.Longitude
            }
        }
            };

            if (model.Photos != null && model.Photos.Any())
            {
                string uploadPath = Path.Combine(_env.WebRootPath, "photos");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var file in model.Photos)
                {
                    if (file.Length == 0)
                        continue;

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var ext = Path.GetExtension(file.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                        continue;

                    string fileName = Guid.NewGuid() + ext;
                    string fullPath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    apartment.Photos.Add(new Photo
                    {
                        PhotoPath = "/photos/" + fileName
                    });
                }
            }

            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyApartments));
        }


        // Редактирование квартиры
        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            var address = apartment.Addresses?.FirstOrDefault();
            var viewModel = new ApartmentViewModel
            {
                ApartmentId = apartment.ApartmentId,
                Description = apartment.Description,
                Price = apartment.Price ?? 0,
                Size = apartment.Size ?? 0,
                Rooms = apartment.Rooms ?? 0,
                StreetAddress = address?.StreetAddress,
                BuildingNumber = address?.BuildingNumber,
                ApartmentNumber = address?.ApartmentNumber,
                District = address?.District,
                City = address?.City,
                Region = address?.Region,
                Latitude = address?.Latitude,       // ✅ Для карты Edit
                Longitude = address?.Longitude,    // ✅ Для карты Edit
                PhotoPaths = apartment.Photos?.Select(p => p.PhotoPath).ToList() ?? new()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ApartmentViewModel model)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            // ✅ Обновить основные параметры
            apartment.Description = model.Description;
            apartment.Price = model.Price;
            apartment.Size = model.Size;
            apartment.Rooms = model.Rooms;

            // ✅ Обновить адрес + координаты
            var address = apartment.Addresses?.FirstOrDefault();
            if (address != null)
            {
                address.StreetAddress = model.StreetAddress;
                address.BuildingNumber = model.BuildingNumber;
                address.ApartmentNumber = model.ApartmentNumber;
                address.District = model.District;
                address.City = model.City;
                address.Region = model.Region;
                address.Latitude = model.Latitude;     // ✅ Новые с карты
                address.Longitude = model.Longitude;   // ✅ Новые с карты
            }

            // ✅ Добавить новые фото в /photos
            if (model.Photos != null && model.Photos.Any())
            {
                string uploadPath = Path.Combine(_env.WebRootPath, "photos");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var file in model.Photos)
                {
                    if (file.Length == 0)
                        continue;

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var ext = Path.GetExtension(file.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                        continue;

                    string fileName = Guid.NewGuid() + ext;
                    string fullPath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    apartment.Photos.Add(new Photo
                    {
                        PhotoPath = "/photos/" + fileName
                    });
                }
            }

            _context.SaveChanges();
            return RedirectToAction("MyApartments");
        }

        // Удаление квартиры
        [HttpGet]
        public IActionResult Delete(int id)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            var address = apartment.Addresses?.FirstOrDefault();
            var viewModel = new ApartmentViewModel
            {
                ApartmentId = apartment.ApartmentId,
                Description = apartment.Description,
                Price = apartment.Price ?? 0,
                Size = apartment.Size ?? 0,
                Rooms = apartment.Rooms ?? 0,
                StreetAddress = address?.StreetAddress,
                BuildingNumber = address?.BuildingNumber,
                District = address?.District,
                City = address?.City,
                PhotoPaths = apartment.Photos?.Select(p => p.PhotoPath).ToList() ?? new(),
                ReviewCount = apartment.ReviewApartments.Count,
                Latitude = address?.Latitude,       // ✅ Координаты
                Longitude = address?.Longitude     // ✅ Координаты
            };

            return View(viewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                    .ThenInclude(ad => ad.Appointments)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            // ✅ 1️⃣ Удалить фото из файловой системы
            if (apartment.Photos != null && apartment.Photos.Any())
            {
                foreach (var photo in apartment.Photos)
                {
                    if (!string.IsNullOrEmpty(photo.PhotoPath))
                    {
                        string filePath = Path.Combine(_env.WebRootPath, photo.PhotoPath.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }
            }

            // ✅ 2️⃣ DELETE appointments (зависит от Address)
            foreach (var address in apartment.Addresses ?? new List<Address>())
            {
                _context.Appointments.RemoveRange(address.Appointments ?? new List<Appointment>());
            }

            // ✅ 3️⃣ Delete photos from DB
            _context.Photos.RemoveRange(apartment.Photos ?? new List<Photo>());

            // ✅ 4️⃣ Delete reviews
            _context.ReviewApartments.RemoveRange(apartment.ReviewApartments ?? new List<ReviewApartment>());

            // ✅ 5️⃣ Delete addresses
            _context.Addresses.RemoveRange(apartment.Addresses ?? new List<Address>());

            // ✅ 6️⃣ Delete apartment
            _context.Apartments.Remove(apartment);

            _context.SaveChanges();

            return RedirectToAction(nameof(MyApartments));
     
        }

        private async Task<bool> IsPremiumLandlord(int userId)
        {
            var sub = await _context.LandlordSubscriptions
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (sub == null) return false;

            if (sub.Status != "active") return false;

            return true;
        }

        private async Task<bool> CanAddApartment(int userId)
        {
            var count = await _context.Apartments
                .CountAsync(a => a.UserId == userId);

            if (count == 0) return true;

            var premium = await IsPremiumLandlord(userId);

            return premium;
        }
    }


}
