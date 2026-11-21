using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;

namespace ASI.Basecode.Services.Mapping
{
    /// <summary>AutoMapper maps between EF entities and service view models.</summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Transaction monitoring
            this.CreateMap<ASI.Basecode.Data.QueryModels.TransactionRow, TransactionRowViewModel>();

            // Equipment administration
            this.CreateMap<Equipment, EquipmentListViewModel>()
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.EquipmentCategory.CategoryName))
                .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.LocationName : null))
                .ForMember(d => d.ConditionName, o => o.MapFrom(s => s.CurrentCondition.Description))
                .ForMember(d => d.OperationalStatusName, o => o.MapFrom(s => s.OperationalStatus.Description));

            this.CreateMap<Equipment, EquipmentFormViewModel>()
                .ForMember(d => d.TotalQuantity, o => o.Ignore());

            this.CreateMap<EquipmentFormViewModel, Equipment>()
                .ForMember(d => d.EquipmentId, o => o.Ignore())
                .ForMember(d => d.CreatedAtUtc, o => o.Ignore())
                .ForMember(d => d.RowVersion, o => o.Ignore())
                .ForMember(d => d.IsActive, o => o.MapFrom(s => s.IsActive));

            // User management
            this.CreateMap<User, UserListViewModel>()
                .ForMember(d => d.RoleName, o => o.MapFrom(s => s.Role.RoleName))
                .ForMember(d => d.RoleCode, o => o.MapFrom(s => s.Role.RoleCode))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.DepartmentName : null))
                .ForMember(d => d.AccountStatusCode, o => o.MapFrom(s => s.AccountStatus.Code))
                .ForMember(d => d.AccountStatusDescription, o => o.MapFrom(s => s.AccountStatus.Description));

            this.CreateMap<User, UserFormViewModel>();

            this.CreateMap<UserFormViewModel, User>()
                .ForMember(d => d.UserId, o => o.Ignore())
                .ForMember(d => d.CreatedAtUtc, o => o.Ignore())
                .ForMember(d => d.RowVersion, o => o.Ignore())
                .ForMember(d => d.PasswordHash, o => o.Ignore());

            // Borrower reservations
            this.CreateMap<Equipment, EquipmentAvailabilityRowViewModel>()
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.EquipmentCategory.CategoryName))
                .ForMember(d => d.LocationName, o => o.MapFrom(s => s.Location != null ? s.Location.LocationName : null))
                .ForMember(d => d.ConditionName, o => o.MapFrom(s => s.CurrentCondition.Description))
                .ForMember(d => d.OperationalStatusName, o => o.MapFrom(s => s.OperationalStatus.Description));

            this.CreateMap<Reservation, ReservationListViewModel>()
                .ForMember(d => d.EquipmentName, o => o.MapFrom(s => s.Equipment.EquipmentName))
                .ForMember(d => d.AssetCode, o => o.MapFrom(s => s.Equipment.AssetCode))
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Equipment.EquipmentCategory.CategoryName))
                .ForMember(d => d.PickupLocationName, o => o.MapFrom(s => s.PickupLocation != null ? s.PickupLocation.LocationName : null))
                .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
                .ForMember(d => d.StatusDescription, o => o.MapFrom(s => s.Status.Description))
                .ForMember(d => d.CanCancel, o => o.Ignore());

            this.CreateMap<Reservation, ReservationDetailsViewModel>()
                .ForMember(d => d.EquipmentName, o => o.MapFrom(s => s.Equipment.EquipmentName))
                .ForMember(d => d.AssetCode, o => o.MapFrom(s => s.Equipment.AssetCode))
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Equipment.EquipmentCategory.CategoryName))
                .ForMember(d => d.PickupLocationName, o => o.MapFrom(s => s.PickupLocation != null ? s.PickupLocation.LocationName : null))
                .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
                .ForMember(d => d.StatusDescription, o => o.MapFrom(s => s.Status.Description))
                .ForMember(d => d.BorrowerName, o => o.MapFrom(s => s.BorrowerUser != null
                    ? s.BorrowerUser.FirstName + " " + s.BorrowerUser.LastName
                    : null))
                .ForMember(d => d.PolicyVersionNumber, o => o.MapFrom(s => s.BorrowingPolicyVersion != null ? s.BorrowingPolicyVersion.VersionNumber : null))
                .ForMember(d => d.CanCancel, o => o.Ignore());
        }
    }
}
