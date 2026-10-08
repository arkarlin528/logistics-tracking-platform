using FluentValidation;

namespace Logistics.Application.Shipments;

public sealed class ShipmentFilterValidator : AbstractValidator<ShipmentFilter>
{
    public ShipmentFilterValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    public CreateShipmentRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.CarrierId).GreaterThan(0);
        RuleFor(x => x.OriginPortCode).NotEmpty().Length(5);
        RuleFor(x => x.DestinationPortCode).NotEmpty().Length(5)
            .NotEqual(x => x.OriginPortCode, StringComparer.OrdinalIgnoreCase)
            .WithMessage("Destination must be different from origin.");
        RuleFor(x => x.CargoDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.WeightKg).GreaterThan(0).LessThanOrEqualTo(500_000);
    }
}

public sealed class RecordEventRequestValidator : AbstractValidator<RecordEventRequest>
{
    public RecordEventRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x)
            .Must(x => (x.Latitude is null) == (x.Longitude is null))
            .WithName("Coordinates")
            .WithMessage("Latitude and longitude must be given together.");
    }
}

public sealed class ReviseEtaRequestValidator : AbstractValidator<ReviseEtaRequest>
{
    public ReviseEtaRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}
