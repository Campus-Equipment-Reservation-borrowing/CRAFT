using ASI.Basecode.Data;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Equipment Administration screens (Figma: A - Equipment Administration).
    /// Thin controller: binds requests, delegates to IEquipmentService, prepares view data.
    /// </summary>
    [Authorize(Roles = RoleCodes.Staff)]
    public class EquipmentController : ControllerBase<EquipmentController>
    {
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxImageBytes = 5 * 1024 * 1024;

        private readonly IEquipmentService _equipmentService;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _environment;

        public EquipmentController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IEquipmentService equipmentService,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._equipmentService = equipmentService;
            this._environment = environment;
        }

        /// <summary>
        /// Equipment list with search, codebook filters and paging.
        /// </summary>
        public IActionResult Index(
            string search,
            long? category,
            long? location,
            long? condition,
            long? operationalStatus,
            int page = 1,
            int pageSize = 10)
        {
            try
            {
                var model = this._equipmentService.GetIndexViewModel(
                    search, category, location, condition, operationalStatus, page, pageSize);

                this.ViewBag.CategoryOptions = this.ToSelectList(
                    this._equipmentService.GetCategoryOptions(), model.CategoryId, "All Categories");
                this.ViewBag.LocationOptions = this.ToSelectList(
                    this._equipmentService.GetLocationOptions(), model.LocationId, "All Locations");
                this.ViewBag.ConditionOptions = this.ToSelectList(
                    this._equipmentService.GetConditionOptions(), model.ConditionId, "All Conditions");
                this.ViewBag.OperationalStatusOptions = this.ToSelectList(
                    this._equipmentService.GetConditionOptions(), model.OperationalStatusId, "All Statuses");

                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Index");
                this.TempData["ErrorMessage"] = "Unable to load the equipment list.";
                return this.View(new EquipmentIndexViewModel { Page = new PagedResult<EquipmentListViewModel>() });
            }
        }

        public IActionResult Create()
        {
            this.ViewBag.CategoryOptions = this.ToSelectList(this._equipmentService.GetCategoryOptions(), null, "-- Select category --");
            this.ViewBag.LocationOptions = this.ToSelectList(this._equipmentService.GetLocationOptions(), null, "-- Select location --");
            this.ViewBag.ConditionOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), null, "-- Select condition --");
            this.ViewBag.OperationalStatusOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), null, "-- Select status --");

            return this.View(new EquipmentFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EquipmentFormViewModel form, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var (path, imageError) = this.SaveEquipmentImage(imageFile);
                if (imageError != null)
                {
                    this.ModelState.AddModelError(nameof(form.ImagePath), imageError);
                }
                else
                {
                    form.ImagePath = path;
                }
            }

            if (!this.ModelState.IsValid)
            {
                this.ViewBag.CategoryOptions = this.ToSelectList(this._equipmentService.GetCategoryOptions(), form.EquipmentCategoryId, "-- Select category --");
                this.ViewBag.LocationOptions = this.ToSelectList(this._equipmentService.GetLocationOptions(), form.LocationId, "-- Select location --");
                this.ViewBag.ConditionOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
                this.ViewBag.OperationalStatusOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.OperationalStatusId, "-- Select status --");
                return this.View(form);
            }

            var (success, error, _) = this._equipmentService.Save(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                this.ViewBag.CategoryOptions = this.ToSelectList(this._equipmentService.GetCategoryOptions(), form.EquipmentCategoryId, "-- Select category --");
                this.ViewBag.LocationOptions = this.ToSelectList(this._equipmentService.GetLocationOptions(), form.LocationId, "-- Select location --");
                this.ViewBag.ConditionOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
                this.ViewBag.OperationalStatusOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.OperationalStatusId, "-- Select status --");
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "Equipment record created.";
            return this.RedirectToAction(nameof(this.Index));
        }

        public IActionResult Edit(long id)
        {
            var form = this._equipmentService.GetForEdit(id);
            if (form == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.EquipmentNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            this.ViewBag.CategoryOptions = this.ToSelectList(this._equipmentService.GetCategoryOptions(), form.EquipmentCategoryId, "-- Select category --");
            this.ViewBag.LocationOptions = this.ToSelectList(this._equipmentService.GetLocationOptions(), form.LocationId, "-- Select location --");
            this.ViewBag.ConditionOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
            this.ViewBag.OperationalStatusOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.OperationalStatusId, "-- Select status --");

            return this.View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(long id, EquipmentFormViewModel form, IFormFile imageFile, bool removeImage = false)
        {
            form.EquipmentId = id;

            if (removeImage)
            {
                form.ImagePath = null;
            }
            else if (imageFile != null && imageFile.Length > 0)
            {
                var (path, imageError) = this.SaveEquipmentImage(imageFile);
                if (imageError != null)
                {
                    this.ModelState.AddModelError(nameof(form.ImagePath), imageError);
                }
                else
                {
                    form.ImagePath = path;
                }
            }

            if (!this.ModelState.IsValid)
            {
                this.ViewBag.CategoryOptions = this.ToSelectList(this._equipmentService.GetCategoryOptions(), form.EquipmentCategoryId, "-- Select category --");
                this.ViewBag.LocationOptions = this.ToSelectList(this._equipmentService.GetLocationOptions(), form.LocationId, "-- Select location --");
                this.ViewBag.ConditionOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
                this.ViewBag.OperationalStatusOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.OperationalStatusId, "-- Select status --");
                return this.View(form);
            }

            var (success, error, _) = this._equipmentService.Save(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                this.ViewBag.CategoryOptions = this.ToSelectList(this._equipmentService.GetCategoryOptions(), form.EquipmentCategoryId, "-- Select category --");
                this.ViewBag.LocationOptions = this.ToSelectList(this._equipmentService.GetLocationOptions(), form.LocationId, "-- Select location --");
                this.ViewBag.ConditionOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
                this.ViewBag.OperationalStatusOptions = this.ToSelectList(this._equipmentService.GetConditionOptions(), form.OperationalStatusId, "-- Select status --");
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "Equipment record updated.";
            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>
        /// Read-only equipment detail view (inventory summary + status info).
        /// </summary>
        public IActionResult Details(long id)
        {
            var entity = this._equipmentService.GetByIdWithDetails(id);
            if (entity == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.EquipmentNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(entity);
        }

        /// <summary>
        /// Printable QR label. The code opens Custodian/Scan for the asset, which lists the
        /// releases and returns waiting on it.
        /// </summary>
        public IActionResult Label(long id)
        {
            var entity = this._equipmentService.GetByIdWithDetails(id);
            if (entity == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.EquipmentNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            var scanUrl = this.Url.Action("Scan", "Custodian", new { code = entity.AssetCode }, this.Request.Scheme);
            this.ViewBag.QrSvg = QrCode.ToSvg(scanUrl);
            return this.View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(long id)
        {
            var (success, error) = this._equipmentService.Delete(id);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Equipment record deleted.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        private (string Path, string Error) SaveEquipmentImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return (null, null);
                }

                if (file.Length > MaxImageBytes)
                {
                    return (null, "Image must be 5 MB or smaller.");
                }

                var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
                {
                    return (null, "Upload a JPG, PNG, WEBP or GIF image.");
                }

                var folder = Path.Combine(this._environment.WebRootPath, "uploads", "equipment");
                Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid().ToString("N") + extension;
                var absolutePath = Path.Combine(folder, fileName);
                using (var stream = new FileStream(absolutePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                return ("/uploads/equipment/" + fileName, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save equipment image.");
                return (null, "Unable to save the uploaded image. Please try again.");
            }
        }

        private SelectList ToSelectList(System.Collections.Generic.IEnumerable<LookupViewModel> options, long? selected, string emptyText)
        {
            var items = options
                .Select(o => new { Id = o.Id.ToString(), o.Text })
                .ToList();

            // emptyText row first
            var withEmpty = new[] { new { Id = string.Empty, Text = emptyText } }.Concat(items);

            return new SelectList(withEmpty, "Id", "Text", selected?.ToString());
        }
    }
}
