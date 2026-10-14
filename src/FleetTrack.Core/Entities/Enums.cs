namespace FleetTrack.Core.Entities;

// Enums are stored in Oracle as readable strings (e.g. 'ProxyId'), not numbers,
// so the data still makes sense when queried directly in SQL Developer.

public enum HolderType
{
    Company,
    Individual
}

public enum DocumentType
{
    Rc1,
    Brnc,
    ProxyLetter,
    ProxyId
}

public enum WorkType
{
    RoutineService,
    Repair,
    Tyres,
    Brakes,
    Electrical,
    Roadworthy,
    Other
}

public enum RenewalStepType
{
    DiskHandedToDriver,
    DiskFittedToVehicle,
    CopyFiled,
    Other
}

public enum AuditAction
{
    Create,
    Update,
    Delete,
    View,
    Download
}
