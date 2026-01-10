using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentControlSystem.Migrations
{
    /// <inheritdoc />
    public partial class updateinitialcreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Occupancies_Properties_PropertyId",
                table: "Occupancies");

            migrationBuilder.DropIndex(
                name: "IX_Occupancies_PropertyId",
                table: "Occupancies");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TenancyHistories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "TenancyHistories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "NextPaymentDate",
                table: "TenancyAgreements",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComputedColumnSql: "CASE WHEN Status = 'Active' THEN DATEADD(MONTH, DATEDIFF(MONTH, StartDate, GETDATE()) + 1, StartDate) ELSE NULL END");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Occupancies",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "Mediators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Specialization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Qualifications = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: false),
                    SuccessRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxActiveCases = table.Column<int>(type: "int", nullable: false),
                    CurrentActiveCases = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mediators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCDOfficers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanPresideHearings = table.Column<bool>(type: "bit", nullable: false),
                    CanAssignCases = table.Column<bool>(type: "bit", nullable: false),
                    CanCloseCases = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCDOfficers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CaseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ComplainantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ComplainantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ComplainantPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ComplainantEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RespondentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RespondentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RespondentPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    RespondentEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenancyAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    IncidentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AwardedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResolutionDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedOfficerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedMediatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedMediatorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cases_Mediators_AssignedMediatorId",
                        column: x => x.AssignedMediatorId,
                        principalTable: "Mediators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_Mediators_MediatorId",
                        column: x => x.MediatorId,
                        principalTable: "Mediators",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cases_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_RCDOfficers_AssignedOfficerId",
                        column: x => x.AssignedOfficerId,
                        principalTable: "RCDOfficers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_RCDOfficers_OfficerId",
                        column: x => x.OfficerId,
                        principalTable: "RCDOfficers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cases_TenancyAgreements_TenancyAgreementId",
                        column: x => x.TenancyAgreementId,
                        principalTable: "TenancyAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseCommunications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunicationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SenderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SenderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SenderPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    RecipientId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseCommunications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseCommunications_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseDocuments_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseNotes_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParticipantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParticipantEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParticipantPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ParticipantType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseParticipants_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseUpdates_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hearings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HearingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HearingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VirtualMeetingLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Scheduled"),
                    PresidingOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PresidingOfficerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClerkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClerkName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Minutes = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    MinutesFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PresidingOfficerId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hearings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hearings_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hearings_RCDOfficers_PresidingOfficerId",
                        column: x => x.PresidingOfficerId,
                        principalTable: "RCDOfficers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hearings_RCDOfficers_PresidingOfficerId1",
                        column: x => x.PresidingOfficerId1,
                        principalTable: "RCDOfficers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MediationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VirtualMeetingLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedMediatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedMediatorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgreementReached = table.Column<bool>(type: "bit", nullable: true),
                    AgreementSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgreementDocumentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediatorNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutcomeSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplainantSatisfied = table.Column<bool>(type: "bit", nullable: true),
                    RespondentSatisfied = table.Column<bool>(type: "bit", nullable: true),
                    ComplainantFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespondentFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediationSessions_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediationSessions_Mediators_AssignedMediatorId",
                        column: x => x.AssignedMediatorId,
                        principalTable: "Mediators",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HearingParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HearingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParticipantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParticipantEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParticipantPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ParticipantType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    HasConfirmedAttendance = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attended = table.Column<bool>(type: "bit", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HearingParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HearingParticipants_Hearings_HearingId",
                        column: x => x.HearingId,
                        principalTable: "Hearings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediationDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsConfidential = table.Column<bool>(type: "bit", nullable: false),
                    ConfidentialityLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediationDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediationDocument_MediationSessions_MediationSessionId",
                        column: x => x.MediationSessionId,
                        principalTable: "MediationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParticipantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParticipantEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParticipantPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CasePartyType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false),
                    Organization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepresentationAuthority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    HasConfirmedAttendance = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attended = table.Column<bool>(type: "bit", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SpecialRequirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediationParticipants_MediationSessions_MediationSessionId",
                        column: x => x.MediationSessionId,
                        principalTable: "MediationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenancyHistories_ChangedAt",
                table: "TenancyHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Occupancies_PropertyId_IsCurrent",
                table: "Occupancies",
                columns: new[] { "PropertyId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_Occupancies_TenantId",
                table: "Occupancies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseCommunications_CaseId_CreatedAt",
                table: "CaseCommunications",
                columns: new[] { "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseCommunications_RecipientId_IsRead_CreatedAt",
                table: "CaseCommunications",
                columns: new[] { "RecipientId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_CaseId_DocumentType",
                table: "CaseDocuments",
                columns: new[] { "CaseId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseNotes_CaseId_CreatedAt",
                table: "CaseNotes",
                columns: new[] { "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseParticipants_CaseId_ParticipantId",
                table: "CaseParticipants",
                columns: new[] { "CaseId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_AssignedMediatorId",
                table: "Cases",
                column: "AssignedMediatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_AssignedOfficerId",
                table: "Cases",
                column: "AssignedOfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseNumber",
                table: "Cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_ComplainantId_Status",
                table: "Cases",
                columns: new[] { "ComplainantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CreatedAt_Priority",
                table: "Cases",
                columns: new[] { "CreatedAt", "Priority" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_MediatorId",
                table: "Cases",
                column: "MediatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OfficerId",
                table: "Cases",
                column: "OfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_PropertyId",
                table: "Cases",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_RespondentId_Status",
                table: "Cases",
                columns: new[] { "RespondentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Status_CreatedAt",
                table: "Cases",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenancyAgreementId",
                table: "Cases",
                column: "TenancyAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseUpdates_CaseId_CreatedAt",
                table: "CaseUpdates",
                columns: new[] { "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HearingParticipants_HearingId_ParticipantId",
                table: "HearingParticipants",
                columns: new[] { "HearingId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hearings_CaseId_HearingDate",
                table: "Hearings",
                columns: new[] { "CaseId", "HearingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Hearings_HearingDate_Status",
                table: "Hearings",
                columns: new[] { "HearingDate", "Status" },
                filter: "[Status] = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "IX_Hearings_HearingNumber",
                table: "Hearings",
                column: "HearingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hearings_PresidingOfficerId_HearingDate",
                table: "Hearings",
                columns: new[] { "PresidingOfficerId", "HearingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Hearings_PresidingOfficerId1",
                table: "Hearings",
                column: "PresidingOfficerId1");

            migrationBuilder.CreateIndex(
                name: "IX_MediationDocument_MediationSessionId",
                table: "MediationDocument",
                column: "MediationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MediationParticipants_MediationSessionId",
                table: "MediationParticipants",
                column: "MediationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MediationSessions_AssignedMediatorId",
                table: "MediationSessions",
                column: "AssignedMediatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MediationSessions_CaseId",
                table: "MediationSessions",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Mediators_LicenseNumber",
                table: "Mediators",
                column: "LicenseNumber",
                unique: true,
                filter: "[LicenseNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Mediators_UserId",
                table: "Mediators",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCDOfficers_EmployeeNumber",
                table: "RCDOfficers",
                column: "EmployeeNumber",
                unique: true,
                filter: "[EmployeeNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RCDOfficers_UserId",
                table: "RCDOfficers",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Occupancies_Properties_PropertyId",
                table: "Occupancies",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Occupancies_Properties_PropertyId",
                table: "Occupancies");

            migrationBuilder.DropTable(
                name: "CaseCommunications");

            migrationBuilder.DropTable(
                name: "CaseDocuments");

            migrationBuilder.DropTable(
                name: "CaseNotes");

            migrationBuilder.DropTable(
                name: "CaseParticipants");

            migrationBuilder.DropTable(
                name: "CaseUpdates");

            migrationBuilder.DropTable(
                name: "HearingParticipants");

            migrationBuilder.DropTable(
                name: "MediationDocument");

            migrationBuilder.DropTable(
                name: "MediationParticipants");

            migrationBuilder.DropTable(
                name: "Hearings");

            migrationBuilder.DropTable(
                name: "MediationSessions");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "Mediators");

            migrationBuilder.DropTable(
                name: "RCDOfficers");

            migrationBuilder.DropIndex(
                name: "IX_TenancyHistories_ChangedAt",
                table: "TenancyHistories");

            migrationBuilder.DropIndex(
                name: "IX_Occupancies_PropertyId_IsCurrent",
                table: "Occupancies");

            migrationBuilder.DropIndex(
                name: "IX_Occupancies_TenantId",
                table: "Occupancies");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TenancyHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "TenancyHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Occupancies",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "NextPaymentDate",
                table: "TenancyAgreements",
                type: "datetime2",
                nullable: true,
                computedColumnSql: "CASE WHEN Status = 'Active' THEN DATEADD(MONTH, DATEDIFF(MONTH, StartDate, GETDATE()) + 1, StartDate) ELSE NULL END",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Occupancies_PropertyId",
                table: "Occupancies",
                column: "PropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Occupancies_Properties_PropertyId",
                table: "Occupancies",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
