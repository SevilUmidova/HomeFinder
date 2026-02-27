using HomeFinder.Context;
using HomeFinder.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HomeFinder.Controllers
{
    public class ReportController : Controller
    {
        private readonly HomeFinderContext _context;

        public ReportController(HomeFinderContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult MostViewedApartments(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostViewedApartmentsVm(top, from, to);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostViewedApartmentsData(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostViewedApartmentsVm(top, from, to);
            return PartialView("_MostViewedApartmentsDataResponse", vm);
        }

        public IActionResult MostViewedDistricts(int top = 5)
        {
            top = ClampTop(top);

            var districtApartments = _context.Addresses
                .AsNoTracking()
                .Where(ad => ad.ApartmentId != null && ad.District != null && ad.District != "")
                .GroupBy(ad => new { ad.District, ad.ApartmentId })
                .Select(g => new
                {
                    District = g.Key.District!,
                    ApartmentId = g.Key.ApartmentId!.Value
                });

            var items = districtApartments
                .Join(_context.Apartments.AsNoTracking(),
                    da => da.ApartmentId,
                    ap => ap.ApartmentId,
                    (da, ap) => new
                    {
                        da.District,
                        ApartmentId = ap.ApartmentId,
                        Views = ap.Views ?? 0
                    })
                .GroupBy(x => x.District)
                .Select(g => new MostViewedDistrictsReportVm.Row
                {
                    District = g.Key,
                    TotalViews = g.Sum(x => x.Views),
                    ApartmentsCount = g.Select(x => x.ApartmentId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(top)
                .ToList();

            return View(new MostViewedDistrictsReportVm
            {
                Top = top,
                Items = items
            });
        }

        private MostViewedApartmentsReportVm BuildMostViewedApartmentsVm(int top, DateTime fromDate, DateTime toDate)
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

            var apartments = _context.Apartments
                .AsNoTracking()
                .Where(a => apartmentIds.Contains(a.ApartmentId))
                .Select(a => new MostViewedApartmentsReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = 0,
                    Price = a.Price,
                    District = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.District).FirstOrDefault(),
                    City = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.City).FirstOrDefault(),
                    StreetAddress = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.StreetAddress).FirstOrDefault(),
                    BuildingNumber = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.BuildingNumber).FirstOrDefault(),
                })
                .ToList();

            var viewCounts = topByViews.ToDictionary(x => x.ApartmentId, x => x.Views);
            foreach (var row in apartments)
            {
                row.Views = viewCounts.TryGetValue(row.ApartmentId, out var c) ? c : 0;
            }
            var items = apartments.OrderByDescending(r => r.Views).ToList();

            return new MostViewedApartmentsReportVm
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
                var tmp = from;
                from = to;
                to = tmp;
            }

            return (from, to);
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (DateTime.TryParseExact(
                value.Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
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
    }
}