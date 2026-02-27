using HomeFinder.Context;
using HomeFinder.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public IActionResult MostViewedApartments(int top = 5, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(dateFrom, dateTo);

            var vm = BuildMostViewedApartmentsVm(top, from, to);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostViewedApartmentsData(int top = 5, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(dateFrom, dateTo);

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
            var items = _context.Apartments
                .AsNoTracking()
                .OrderByDescending(a => a.Views ?? 0)
                .Select(a => new MostViewedApartmentsReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = a.Views ?? 0,
                    Price = a.Price,

                    District = a.Addresses
                        .OrderBy(ad => ad.AddressId)
                        .Select(ad => ad.District)
                        .FirstOrDefault(),

                    City = a.Addresses
                        .OrderBy(ad => ad.AddressId)
                        .Select(ad => ad.City)
                        .FirstOrDefault(),

                    StreetAddress = a.Addresses
                        .OrderBy(ad => ad.AddressId)
                        .Select(ad => ad.StreetAddress)
                        .FirstOrDefault(),

                    BuildingNumber = a.Addresses
                        .OrderBy(ad => ad.AddressId)
                        .Select(ad => ad.BuildingNumber)
                        .FirstOrDefault(),
                })
                .Take(top)
                .ToList();

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

        private static int ClampTop(int top)
        {
            if (top <= 0) return 5;
            if (top > 50) return 50;
            return top;
        }
    }
}