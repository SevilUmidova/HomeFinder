using HomeFinder.Context;
using HomeFinder.Models;
using HomeFinder.Models.Reports;
using HomeFinder.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using Stripe;
using System.Globalization;

namespace HomeFinder.Controllers
{
    [ApiController]
    [Route("api")]
    public class SpaApiController : ControllerBase
    {
        private readonly HomeFinderContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;
        private readonly IAntiforgery _antiforgery;

        public SpaApiController(
            HomeFinderContext context,
            IWebHostEnvironment env,
            IConfiguration cfg,
            IAntiforgery antiforgery)
        {
            _context = context;
            _env = env;
            _cfg = cfg;
            _antiforgery = antiforgery;
        }

        [HttpGet("security/csrf")]
        public IActionResult GetCsrfToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { requestToken = tokens.RequestToken });
        }

        [HttpGet("auth/me")]
        public IActionResult Me()
        {
            var role = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetInt32("UserId");
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var isAuthenticated = !string.IsNullOrWhiteSpace(role);

            return Ok(new
            {
                isAuthenticated,
                role,
                userId,
                adminId,
                userName = HttpContext.Session.GetString("UserName"),
                isPremium = HttpContext.Session.GetString("IsPremium") == "1"
            });
        }

        [HttpPost("auth/login")]
        public IActionResult Login([FromBody] LoginViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserType) || string.IsNullOrWhiteSpace(model.Login) || string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { message = "Заполните логин, пароль и тип пользователя." });
            }

            if (model.UserType == "admin")
            {
                var admin = _context.Administrators.FirstOrDefault(a => a.Login == model.Login);
                if (admin == null || string.IsNullOrWhiteSpace(admin.Password))
                {
                    return Unauthorized(new { message = "Неверный логин или пароль администратора." });
                }

                var adminOk = PasswordHasher.Verify(model.Password, admin.Password);
                if (!adminOk && admin.Password == model.Password)
                {
                    admin.Password = PasswordHasher.Hash(model.Password);
                    _context.SaveChanges();
                    adminOk = true;
                }

                if (!adminOk)
                {
                    return Unauthorized(new { message = "Неверный логин или пароль администратора." });
                }

                HttpContext.Session.Clear();
                HttpContext.Session.SetInt32("AdminId", admin.AdministratorId);
                HttpContext.Session.SetString("UserRole", "Admin");

                return Ok(new { role = "Admin", redirectTo = "/admin" });
            }

            var user = _context.Users.FirstOrDefault(u => u.Login == model.Login);
            if (user == null || string.IsNullOrWhiteSpace(user.Password))
            {
                return Unauthorized(new { message = "Неверный логин или пароль." });
            }

            var userOk = PasswordHasher.Verify(model.Password, user.Password);
            if (!userOk && user.Password == model.Password)
            {
                user.Password = PasswordHasher.Hash(model.Password);
                _context.SaveChanges();
                userOk = true;
            }

            if (!userOk)
            {
                return Unauthorized(new { message = "Неверный логин или пароль." });
            }

            if (model.UserType == "landlord" && user.IsLandlord != true)
            {
                return Unauthorized(new { message = "Пользователь не является владельцем." });
            }

            if (model.UserType == "tenant" && user.IsTenant != true)
            {
                return Unauthorized(new { message = "Пользователь не является арендатором." });
            }

            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}".Trim());

            if (model.UserType == "landlord")
            {
                HttpContext.Session.SetString("UserRole", "Landlord");
                HttpContext.Session.SetString("IsPremium", IsPremiumLandlord(user.UserId) ? "1" : "0");
                return Ok(new { role = "Landlord", redirectTo = "/my-listings" });
            }

            HttpContext.Session.SetString("UserRole", "Tenant");
            HttpContext.Session.SetString("IsPremium", "0");
            return Ok(new { role = "Tenant", redirectTo = "/" });
        }

        [HttpPost("auth/logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { success = true });
        }

        [HttpGet("catalog")]
        public IActionResult Catalog(
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
            var items = BuildCatalogQuery(priceMin, priceMax, sizeMin, sizeMax, rooms, city, district, address, sortBy, alltext)
                .Take(200)
                .Select(MapApartmentCardExpression())
                .ToList();

            return Ok(new
            {
                items,
                filters = new
                {
                    priceMin,
                    priceMax,
                    sizeMin,
                    sizeMax,
                    rooms,
                    city,
                    district,
                    address,
                    sortBy,
                    alltext
                }
            });
        }

        [HttpPost("catalog/area")]
        public IActionResult FilterByArea([FromBody] MapAreaRequest request)
        {
            if (request?.Polygon == null || request.Polygon.Count < 3)
            {
                return BadRequest(new { message = "Polygon is required." });
            }

            var minLat = request.Polygon.Min(p => p.Lat);
            var maxLat = request.Polygon.Max(p => p.Lat);
            var minLng = request.Polygon.Min(p => p.Lng);
            var maxLng = request.Polygon.Max(p => p.Lng);

            var candidates = _context.Apartments
                .AsNoTracking()
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .Include(a => a.ReviewApartments)
                .Where(a => a.Addresses.Any(ad =>
                    ad.Latitude >= minLat && ad.Latitude <= maxLat &&
                    ad.Longitude >= minLng && ad.Longitude <= maxLng))
                .Select(MapApartmentCardExpression())
                .ToList();

            var filtered = candidates
                .Where(a => a.latitude.HasValue && a.longitude.HasValue && PointInPolygon((double)a.latitude.Value, (double)a.longitude.Value, request.Polygon))
                .ToList();

            return Ok(new { items = filtered });
        }

        [HttpGet("apartments/{id:int}")]
        public IActionResult ApartmentDetails(int id)
        {
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .Include(a => a.ReviewApartments)
                    .ThenInclude(r => r.User)
                .FirstOrDefault(a => a.ApartmentId == id);

            if (apartment == null)
            {
                return NotFound();
            }

            _context.ApartmentViewLogs.Add(new ApartmentViewLog
            {
                ApartmentId = apartment.ApartmentId,
                ViewedAt = DateTime.Now
            });
            _context.SaveChanges();

            var address = apartment.Addresses.FirstOrDefault();
            var totalViews = _context.ApartmentViewLogs.Count(v => v.ApartmentId == id);
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var isFavorited = currentUserId != null &&
                              HttpContext.Session.GetString("UserRole") == "Tenant" &&
                              _context.Favorites.Any(f => f.UserId == currentUserId && f.ApartmentId == id);

            return Ok(new
            {
                apartmentId = apartment.ApartmentId,
                description = apartment.Description,
                price = apartment.Price ?? 0,
                size = apartment.Size ?? 0,
                rooms = apartment.Rooms ?? 0,
                views = totalViews,
                streetAddress = address?.StreetAddress,
                buildingNumber = address?.BuildingNumber,
                apartmentNumber = address?.ApartmentNumber,
                district = address?.District,
                city = address?.City,
                region = address?.Region,
                latitude = address?.Latitude,
                longitude = address?.Longitude,
                photoPaths = apartment.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).ToList(),
                landlordName = apartment.User != null ? $"{apartment.User.FirstName} {apartment.User.LastName}".Trim() : "Unknown",
                phoneNumber = apartment.User?.PhoneNumber,
                averageRating = apartment.ReviewApartments.Any() ? apartment.ReviewApartments.Average(r => r.Rating ?? 0) : 0,
                reviewCount = apartment.ReviewApartments.Count,
                isFavorited,
                reviews = apartment.ReviewApartments
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        reviewId = r.RApartmentId,
                        userName = r.User != null ? r.User.FirstName ?? "Anonymous" : "Anonymous",
                        rating = r.Rating ?? 0,
                        comment = r.Comment,
                        createdAt = r.CreatedAt
                    })
                    .ToList()
            });
        }

        [HttpGet("favorites")]
        public IActionResult Favorites()
        {
            if (!IsTenantLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как арендатор." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var favorites = _context.Favorites
                .Where(f => f.UserId == userId)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.User)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.Addresses)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.Photos)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.ReviewApartments)
                .ToList()
                .Select(f => MapApartmentCard(f.Apartment))
                .ToList();

            return Ok(new { items = favorites });
        }

        [HttpGet("favorites/status/{apartmentId:int}")]
        public IActionResult FavoriteStatus(int apartmentId)
        {
            if (!IsTenantLoggedIn())
            {
                return Ok(new { favorited = false });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var exists = _context.Favorites.Any(f => f.UserId == userId && f.ApartmentId == apartmentId);
            return Ok(new { favorited = exists });
        }

        [HttpPost("favorites/toggle")]
        public IActionResult ToggleFavorite([FromBody] ToggleFavoriteRequest request)
        {
            if (!IsTenantLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как арендатор." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var exists = _context.Favorites.FirstOrDefault(f => f.UserId == userId && f.ApartmentId == request.ApartmentId);

            if (exists == null)
            {
                _context.Favorites.Add(new Favorite
                {
                    UserId = userId,
                    ApartmentId = request.ApartmentId
                });
            }
            else
            {
                _context.Favorites.Remove(exists);
            }

            _context.SaveChanges();
            return Ok(new { favorited = exists == null });
        }

        [HttpPost("reviews")]
        public IActionResult SaveReview([FromBody] SaveReviewRequest request)
        {
            if (!IsTenantLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как арендатор." });
            }

            if (request.Rating < 1 || request.Rating > 5 || string.IsNullOrWhiteSpace(request.Comment))
            {
                return BadRequest(new { message = "Проверьте рейтинг и комментарий." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var existing = _context.ReviewApartments.FirstOrDefault(r => r.UserId == userId && r.ApartmentId == request.ApartmentId);

            if (existing == null)
            {
                _context.ReviewApartments.Add(new ReviewApartment
                {
                    UserId = userId,
                    ApartmentId = request.ApartmentId,
                    Rating = request.Rating,
                    Comment = request.Comment,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                existing.Rating = request.Rating;
                existing.Comment = request.Comment;
                existing.CreatedAt = DateTime.Now;
            }

            _context.SaveChanges();
            return Ok(new { success = true });
        }

        [HttpGet("appointments")]
        public IActionResult Appointments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null || string.IsNullOrWhiteSpace(role))
            {
                return Unauthorized(new { message = "Требуется вход." });
            }

            IQueryable<Appointment> query = _context.Appointments
                .Include(a => a.Apartment)
                    .ThenInclude(apt => apt!.User)
                .Include(a => a.Address)
                .AsQueryable();

            if (role == "Landlord")
            {
                query = query.Where(a => a.Apartment != null && a.Apartment.UserId == userId);
            }

            var items = query
                .OrderByDescending(a => a.DateTime)
                .ToList()
                .Select(a => new
                {
                    appointmentId = a.AppointmentId,
                    apartmentId = a.ApartmentId,
                    apartmentTitle = a.Apartment?.Description,
                    address = a.Address != null ? $"{a.Address.StreetAddress}, {a.Address.BuildingNumber}" : null,
                    city = a.Address?.City,
                    district = a.Address?.District,
                    dateTime = a.DateTime,
                    phoneNumber = a.Apartment?.User?.PhoneNumber
                })
                .ToList();

            return Ok(new { items, role });
        }

        [HttpGet("appointments/options/{apartmentId:int}")]
        public IActionResult AppointmentOptions(int apartmentId)
        {
            if (!IsTenantLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как арендатор." });
            }

            var apartment = _context.Apartments.Include(a => a.Addresses).FirstOrDefault(a => a.ApartmentId == apartmentId);
            if (apartment == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                apartmentId,
                addresses = apartment.Addresses.Select(a => new
                {
                    addressId = a.AddressId,
                    streetAddress = a.StreetAddress,
                    buildingNumber = a.BuildingNumber,
                    city = a.City,
                    district = a.District
                }).ToList()
            });
        }

        [HttpPost("appointments")]
        public IActionResult CreateAppointment([FromBody] CreateAppointmentRequest request)
        {
            if (!IsTenantLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как арендатор." });
            }

            if (request.DateTime <= DateTime.Now)
            {
                return BadRequest(new { message = "Выберите дату и время в будущем." });
            }

            var appointment = new Appointment
            {
                ApartmentId = request.ApartmentId,
                AddressId = request.AddressId,
                DateTime = request.DateTime
            };

            _context.Appointments.Add(appointment);
            _context.SaveChanges();

            return Ok(new { success = true });
        }

        [HttpPost("appointments/{id:int}/cancel")]
        public IActionResult CancelAppointment(int id)
        {
            if (!IsTenantLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как арендатор." });
            }

            var appointment = _context.Appointments.FirstOrDefault(a => a.AppointmentId == id);
            if (appointment == null)
            {
                return NotFound();
            }

            _context.Appointments.Remove(appointment);
            _context.SaveChanges();
            return Ok(new { success = true });
        }

        [HttpGet("landlord/apartments")]
        public IActionResult LandlordApartments()
        {
            if (!IsLandlordLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как владелец." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var items = _context.Apartments
                .Where(a => a.UserId == userId)
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .ToList()
                .Select(MapApartmentCard)
                .ToList();

            return Ok(new
            {
                items,
                canAddApartment = CanAddApartment(userId)
            });
        }

        [HttpGet("landlord/apartments/{id:int}")]
        public IActionResult LandlordApartment(int id)
        {
            if (!IsLandlordLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как владелец." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
            {
                return NotFound();
            }

            var address = apartment.Addresses.FirstOrDefault();
            return Ok(new
            {
                apartmentId = apartment.ApartmentId,
                description = apartment.Description,
                price = apartment.Price ?? 0,
                size = apartment.Size ?? 0,
                rooms = apartment.Rooms ?? 0,
                streetAddress = address?.StreetAddress,
                buildingNumber = address?.BuildingNumber,
                apartmentNumber = address?.ApartmentNumber,
                district = address?.District,
                city = address?.City,
                region = address?.Region,
                latitude = address?.Latitude,
                longitude = address?.Longitude,
                photoPaths = apartment.Photos.Select(p => p.PhotoPath).ToList()
            });
        }

        [HttpPost("landlord/apartments")]
        public async Task<IActionResult> CreateLandlordApartment([FromForm] ApartmentViewModel model)
        {
            if (!IsLandlordLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как владелец." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            if (!CanAddApartment(userId))
            {
                return BadRequest(new { message = "Для добавления нескольких квартир нужен Premium." });
            }

            var apartment = new Apartment
            {
                UserId = userId,
                Description = model.Description,
                Price = model.Price,
                Size = model.Size,
                Rooms = model.Rooms,
                Photos = new List<Photo>(),
                Addresses = new List<HomeFinder.Models.Address>
                {
                    new HomeFinder.Models.Address
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

            await SaveUploadedPhotos(model.Photos, apartment.Photos);
            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, apartmentId = apartment.ApartmentId });
        }

        [HttpPost("landlord/apartments/{id:int}/update")]
        public async Task<IActionResult> UpdateLandlordApartment(int id, [FromForm] ApartmentViewModel model)
        {
            if (!IsLandlordLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как владелец." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
            {
                return NotFound();
            }

            apartment.Description = model.Description;
            apartment.Price = model.Price;
            apartment.Size = model.Size;
            apartment.Rooms = model.Rooms;

            var address = apartment.Addresses.FirstOrDefault();
            if (address != null)
            {
                address.StreetAddress = model.StreetAddress;
                address.BuildingNumber = model.BuildingNumber;
                address.ApartmentNumber = model.ApartmentNumber;
                address.District = model.District;
                address.City = model.City;
                address.Region = model.Region;
                address.Latitude = model.Latitude;
                address.Longitude = model.Longitude;
            }

            await SaveUploadedPhotos(model.Photos, apartment.Photos);
            _context.SaveChanges();

            return Ok(new { success = true });
        }

        [HttpPost("landlord/apartments/{id:int}/delete")]
        public IActionResult DeleteLandlordApartment(int id)
        {
            if (!IsLandlordLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как владелец." });
            }

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                    .ThenInclude(ad => ad.Appointments)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
            {
                return NotFound();
            }

            DeleteApartmentGraph(apartment);
            return Ok(new { success = true });
        }

        [HttpPost("payment/create-premium-checkout")]
        public IActionResult CreatePremiumCheckout()
        {
            if (!IsLandlordLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как владелец." });
            }

            StripeConfiguration.ApiKey = _cfg["Stripe:SecretKey"];
            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var domain = $"{Request.Scheme}://{Request.Host}";
            var priceId = _cfg["Stripe:LandlordSubscriptionPriceId"];

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                ClientReferenceId = userId.ToString(),
                SuccessUrl = domain + "/Payment/Success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = domain + "/Payment/Cancel",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                }
            };

            var service = new SessionService();
            var session = service.Create(options);
            return Ok(new { checkoutUrl = session.Url });
        }

        [HttpGet("reports/most-viewed-apartments")]
        public IActionResult MostViewedApartmentsReport(int top = 5, string? dateFrom = null, string? dateTo = null, decimal? priceMin = null, decimal? priceMax = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var vm = BuildMostViewedApartmentsVm(top, from, to, priceMin, priceMax);
            return Ok(vm);
        }

        [HttpGet("reports/most-viewed-districts")]
        public IActionResult MostViewedDistrictsReport(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var vm = BuildMostViewedDistrictsVm(top, from, to);
            return Ok(vm);
        }

        [HttpGet("admin/apartments")]
        public IActionResult AdminApartments()
        {
            if (!IsAdminLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как администратор." });
            }

            var items = _context.Apartments
                .Include(a => a.User)
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .ToList()
                .Select(MapApartmentCard)
                .ToList();

            return Ok(new { items });
        }

        [HttpPost("admin/apartments/{id:int}/delete")]
        public IActionResult DeleteAdminApartment(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Unauthorized(new { message = "Требуется вход как администратор." });
            }

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                    .ThenInclude(ad => ad.Appointments)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id);

            if (apartment == null)
            {
                return NotFound();
            }

            DeleteApartmentGraph(apartment);
            return Ok(new { success = true });
        }

        private IQueryable<Apartment> BuildCatalogQuery(
            decimal? priceMin,
            decimal? priceMax,
            int? sizeMin,
            int? sizeMax,
            int? rooms,
            string city,
            string district,
            string address,
            string sortBy,
            string alltext)
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
                         (a.User.LastName != null && a.User.LastName.Contains(text)))));
            }

            query = sortBy switch
            {
                "rating_asc" => query.OrderBy(a => a.ReviewApartments.Any() ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0)) : 0),
                "price_asc" => query.OrderBy(a => a.Price),
                "price_desc" => query.OrderByDescending(a => a.Price),
                "newest" => query.OrderByDescending(a => a.ApartmentId),
                "reviews" => query.OrderByDescending(a => a.ReviewApartments.Count),
                _ => query.OrderByDescending(a => a.ReviewApartments.Any() ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0)) : 0)
            };

            return query;
        }

        private static System.Linq.Expressions.Expression<Func<Apartment, ApartmentCardDto>> MapApartmentCardExpression()
        {
            return a => new ApartmentCardDto
            {
                apartmentId = a.ApartmentId,
                description = a.Description,
                price = a.Price ?? 0,
                size = a.Size ?? 0,
                rooms = a.Rooms ?? 0,
                streetAddress = a.Addresses.OrderBy(x => x.AddressId).Select(x => x.StreetAddress).FirstOrDefault(),
                buildingNumber = a.Addresses.OrderBy(x => x.AddressId).Select(x => x.BuildingNumber).FirstOrDefault(),
                district = a.Addresses.OrderBy(x => x.AddressId).Select(x => x.District).FirstOrDefault(),
                city = a.Addresses.OrderBy(x => x.AddressId).Select(x => x.City).FirstOrDefault(),
                latitude = a.Addresses.OrderBy(x => x.AddressId).Select(x => x.Latitude).FirstOrDefault(),
                longitude = a.Addresses.OrderBy(x => x.AddressId).Select(x => x.Longitude).FirstOrDefault(),
                photoPath = a.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault(),
                landlordName = a.User != null ? ((a.User.FirstName ?? "") + " " + (a.User.LastName ?? "")).Trim() : null,
                phoneNumber = a.User != null ? a.User.PhoneNumber : null,
                averageRating = a.ReviewApartments.Any() ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0)) : 0,
                reviewCount = a.ReviewApartments.Count
            };
        }

        private ApartmentCardDto MapApartmentCard(Apartment apartment)
        {
            var address = apartment.Addresses?.OrderBy(x => x.AddressId).FirstOrDefault();
            return new ApartmentCardDto
            {
                apartmentId = apartment.ApartmentId,
                description = apartment.Description,
                price = apartment.Price ?? 0,
                size = apartment.Size ?? 0,
                rooms = apartment.Rooms ?? 0,
                streetAddress = address?.StreetAddress,
                buildingNumber = address?.BuildingNumber,
                district = address?.District,
                city = address?.City,
                latitude = address?.Latitude,
                longitude = address?.Longitude,
                photoPath = apartment.Photos?.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault(),
                landlordName = apartment.User != null ? $"{apartment.User.FirstName} {apartment.User.LastName}".Trim() : null,
                phoneNumber = apartment.User?.PhoneNumber,
                averageRating = apartment.ReviewApartments.Any() ? apartment.ReviewApartments.Average(r => r.Rating ?? 0) : 0,
                reviewCount = apartment.ReviewApartments.Count
            };
        }

        private async Task SaveUploadedPhotos(List<IFormFile>? files, ICollection<Photo> target)
        {
            if (files == null || !files.Any())
            {
                return;
            }

            var uploadPath = Path.Combine(_env.WebRootPath, "photos");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            foreach (var file in files)
            {
                if (file.Length == 0)
                {
                    continue;
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(ext))
                {
                    continue;
                }

                var fileName = Guid.NewGuid() + ext;
                var fullPath = Path.Combine(uploadPath, fileName);
                await using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);
                target.Add(new Photo { PhotoPath = "/photos/" + fileName });
            }
        }

        private void DeleteApartmentGraph(Apartment apartment)
        {
            if (apartment.Photos != null)
            {
                foreach (var photo in apartment.Photos)
                {
                    if (!string.IsNullOrWhiteSpace(photo.PhotoPath))
                    {
                        var filePath = Path.Combine(_env.WebRootPath, photo.PhotoPath.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }
            }

            foreach (var address in apartment.Addresses ?? Array.Empty<HomeFinder.Models.Address>())
            {
                _context.Appointments.RemoveRange(address.Appointments ?? new List<Appointment>());
            }

            _context.Photos.RemoveRange(apartment.Photos ?? new List<Photo>());
            _context.ReviewApartments.RemoveRange(apartment.ReviewApartments ?? new List<ReviewApartment>());
            _context.Addresses.RemoveRange(apartment.Addresses ?? Array.Empty<HomeFinder.Models.Address>());
            _context.Apartments.Remove(apartment);
            _context.SaveChanges();
        }

        private bool IsTenantLoggedIn() =>
            HttpContext.Session.GetInt32("UserId") != null &&
            HttpContext.Session.GetString("UserRole") == "Tenant";

        private bool IsLandlordLoggedIn() =>
            HttpContext.Session.GetInt32("UserId") != null &&
            HttpContext.Session.GetString("UserRole") == "Landlord";

        private bool IsAdminLoggedIn() =>
            HttpContext.Session.GetInt32("AdminId") != null &&
            HttpContext.Session.GetString("UserRole") == "Admin";

        private bool IsPremiumLandlord(int userId)
        {
            var sub = _context.LandlordSubscriptions.FirstOrDefault(x => x.UserId == userId);
            if (sub == null || !string.Equals(sub.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return sub.CurrentPeriodEndUtc == null || sub.CurrentPeriodEndUtc > DateTime.UtcNow;
        }

        private bool CanAddApartment(int userId)
        {
            var count = _context.Apartments.Count(a => a.UserId == userId);
            return count == 0 || IsPremiumLandlord(userId);
        }

        private static bool PointInPolygon(double lat, double lng, List<MapPointDto> poly)
        {
            var pts = poly.Select(p => new { X = (double)p.Lng, Y = (double)p.Lat }).ToList();
            bool inside = false;

            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                var pi = pts[i];
                var pj = pts[j];
                bool intersect = ((pi.Y > lat) != (pj.Y > lat)) &&
                                 (lng < (pj.X - pi.X) * (lat - pi.Y) / (pj.Y - pi.Y) + pi.X);
                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private MostViewedApartmentsReportVm BuildMostViewedApartmentsVm(int top, DateTime fromDate, DateTime toDate, decimal? priceMin, decimal? priceMax)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var topByViews = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .OrderByDescending(x => x.Views)
                .Take(top)
                .ToList();

            var apartmentIds = topByViews.Select(x => x.ApartmentId).ToList();
            if (apartmentIds.Count == 0)
            {
                return new MostViewedApartmentsReportVm
                {
                    Top = top,
                    DateFrom = fromDate,
                    DateTo = toDate,
                    Items = new List<MostViewedApartmentsReportVm.Row>()
                };
            }

            var apartmentsQuery = _context.Apartments
                .AsNoTracking()
                .Where(a => apartmentIds.Contains(a.ApartmentId));

            if (priceMin.HasValue)
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price >= priceMin.Value);
            }

            if (priceMax.HasValue)
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price <= priceMax.Value);
            }

            var apartments = apartmentsQuery
                .Select(a => new MostViewedApartmentsReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = 0,
                    Price = a.Price,
                    District = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.District).FirstOrDefault(),
                    City = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.City).FirstOrDefault(),
                    StreetAddress = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.StreetAddress).FirstOrDefault(),
                    BuildingNumber = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.BuildingNumber).FirstOrDefault(),
                    PhotoPath = a.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault()
                })
                .ToList();

            var viewCounts = topByViews.ToDictionary(x => x.ApartmentId, x => x.Views);
            foreach (var row in apartments)
            {
                row.Views = viewCounts.TryGetValue(row.ApartmentId, out var c) ? c : 0;
            }

            return new MostViewedApartmentsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                Items = apartments.OrderByDescending(r => r.Views).ToList()
            };
        }

        private MostViewedDistrictsReportVm BuildMostViewedDistrictsVm(int top, DateTime fromDate, DateTime toDate)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var districtApartments = _context.Addresses
                .AsNoTracking()
                .Where(ad => ad.ApartmentId != null && ad.District != null && ad.District != "")
                .Select(ad => new { District = ad.District!, ApartmentId = ad.ApartmentId!.Value })
                .ToList();

            var viewCounts = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);
            var items = districtApartments
                .GroupBy(x => x.District)
                .Select(g => new MostViewedDistrictsReportVm.Row
                {
                    District = g.Key,
                    TotalViews = g.Sum(x => viewCounts.TryGetValue(x.ApartmentId, out var c) ? c : 0),
                    ApartmentsCount = g.Select(x => x.ApartmentId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(top)
                .ToList();

            return new MostViewedDistrictsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                Items = items
            };
        }

        private static (DateTime from, DateTime to) NormalizePeriod(DateTime? dateFrom, DateTime? dateTo)
        {
            var today = DateTime.Today;
            var to = (dateTo ?? today).Date;
            if (to > today) to = today;

            var from = (dateFrom ?? to.AddMonths(-1)).Date;
            if (from > to)
            {
                (from, to) = (to, from);
            }

            return (from, to);
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (DateTime.TryParseExact(value.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private static int ClampTop(int top)
        {
            if (top <= 0) return 5;
            if (top > 50) return 50;
            return top;
        }

        public class ToggleFavoriteRequest
        {
            public int ApartmentId { get; set; }
        }

        public class SaveReviewRequest
        {
            public int ApartmentId { get; set; }
            public int Rating { get; set; }
            public string Comment { get; set; } = string.Empty;
        }

        public class CreateAppointmentRequest
        {
            public int ApartmentId { get; set; }
            public int AddressId { get; set; }
            public DateTime DateTime { get; set; }
        }

        public class ApartmentCardDto
        {
            public int apartmentId { get; set; }
            public string? description { get; set; }
            public decimal price { get; set; }
            public int size { get; set; }
            public int rooms { get; set; }
            public string? streetAddress { get; set; }
            public string? buildingNumber { get; set; }
            public string? district { get; set; }
            public string? city { get; set; }
            public decimal? latitude { get; set; }
            public decimal? longitude { get; set; }
            public string? photoPath { get; set; }
            public string? landlordName { get; set; }
            public string? phoneNumber { get; set; }
            public double averageRating { get; set; }
            public int reviewCount { get; set; }
        }
    }
}
