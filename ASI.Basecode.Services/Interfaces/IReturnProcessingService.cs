using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReturnProcessingService
    {
        ReturnsIndexViewModel GetReturnsIndex(string search, int page, int pageSize);

        ReturnFormContextViewModel GetReturnFormContext(long borrowingId);

        List<LookupViewModel> GetConditionOptions();

        List<LookupViewModel> GetInspectionStatusOptions();

        (bool Success, string Error, long ReturnId) CreateReturn(ReturnFormViewModel form);

        /// <summary>Photos attached to a borrowing's inspections, when the actor may act on it.</summary>
        List<InspectionPhotoViewModel> GetInspectionPhotos(long borrowingId);

        /// <summary>
        /// A stored inspection photo, only for staff allowed to act on that borrowing; (null, null) otherwise.
        /// </summary>
        (string Path, string ContentType) GetInspectionPhotoFile(long inspectionId, string fileName);

        (bool Success, string Error, long InspectionId) CreateInspection(InspectionFormViewModel form, IEnumerable<Microsoft.AspNetCore.Http.IFormFile> photos);
    }
}
