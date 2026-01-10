/*using FluentValidation;
using RentControlSystem.Tenancy.API.DTOs;

namespace RentControlSystem.Tenancy.API.Validators
{
    public class CreateTenancyDtoValidator : AbstractValidator<CreateTenancyDto>
    {
        public CreateTenancyDtoValidator()
        {
            RuleFor(x => x.PropertyId)
                .NotEmpty().WithMessage("Property ID is required");

            RuleFor(x => x.LandlordId)
                .NotEmpty().WithMessage("Landlord ID is required");

            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("Tenant ID is required");

            RuleFor(x => x.MonthlyRent)
                .GreaterThan(0).WithMessage("Monthly rent must be greater than zero");

            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("Start date is required")
                .GreaterThanOrEqualTo(DateTime.Today).WithMessage("Start date cannot be in the past");

            RuleFor(x => x.EndDate)
                .NotEmpty().WithMessage("End date is required")
                .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

            RuleFor(x => x.PaymentFrequency)
                .IsInEnum().WithMessage("Invalid payment frequency");

            RuleFor(x => x.SecurityDeposit)
                .GreaterThanOrEqualTo(0).When(x => x.SecurityDeposit.HasValue)
                .WithMessage("Security deposit cannot be negative");
        }
    }
}*/