using System.ComponentModel.DataAnnotations;
using HomeFinder.Models;

namespace HomeFinder.Models
{
    public class ApartmentViewModel
    {
        public int ApartmentId { get; set; }
        public int? UserId { get; set; }

        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Display(Name = "Цена")]
        public decimal Price { get; set; }

        [Display(Name = "Размер (м²)")]
        public int Size { get; set; }

        [Display(Name = "Количество комнат")]
        public int Rooms { get; set; }

        [Display(Name = "Улица")]
        public string StreetAddress { get; set; }

        [Display(Name = "Номер дома")]
        public string BuildingNumber { get; set; }

        [Display(Name = "Номер квартиры")]
        public string ApartmentNumber { get; set; }

        [Display(Name = "Район")]
        public string District { get; set; }

        [Display(Name = "Город")]
        public string City { get; set; }

        [Display(Name = "Регион")]
        public string Region { get; set; }

        [Display(Name = "Хозяин")]
        public string LandlordName { get; set; }

        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Фото")]
        public List<string> PhotoPaths { get; set; } = new();

        [Display(Name = "Рейтинг")]
        public double AverageRating { get; set; }

        [Display(Name = "Количество отзывов")]
        public int ReviewCount { get; set; }

        [Display(Name = "Количество просмотров")]
        public int? Views { get; set; }

        [Display(Name = "AllText")]
        public string AllText { get; set; }

        // ✅ КРИТИЧНО: Список ВСЕх отзывов для Details страницы
        public List<ReviewApartment> Reviews { get; set; } = new();

        // ✅ Для загрузки фото при создании/редактировании
        [Display(Name = "Загрузить фотографии")]
        public List<IFormFile> Photos { get; set; } = new();

        // ➕ ДЛЯ КАРТЫ (добавляем только это):
        [Display(Name = "Широта")]
        public decimal? Latitude { get; set; }

        [Display(Name = "Долгота")]
        public decimal? Longitude { get; set; }
    }


    // ➕ Новый ViewModel для записи на встречу
    public class AppointmentViewModel
    {
        public int AppointmentId { get; set; }
        public int ApartmentId { get; set; }
        public int AddressId { get; set; }
        public DateTime DateTime { get; set; }

        public List<Address> AvailableAddresses { get; set; } = new();
    }
}
