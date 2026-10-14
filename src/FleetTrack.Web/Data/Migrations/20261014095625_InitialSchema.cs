using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FleetTrack.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ASP_NET_ROLES",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZED_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    CONCURRENCY_STAMP = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_ROLES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_USERS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    USER_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZED_USER_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZED_EMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EMAIL_CONFIRMED = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    PASSWORD_HASH = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    SECURITY_STAMP = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CONCURRENCY_STAMP = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    PHONE_NUMBER = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    PHONE_NUMBER_CONFIRMED = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    TWO_FACTOR_ENABLED = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    LOCKOUT_END = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    LOCKOUT_ENABLED = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    ACCESS_FAILED_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_USERS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AUDIT_LOGS",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIMESTAMP_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(450)", maxLength: 450, nullable: true),
                    USER_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    ACTION = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ENTITY_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ENTITY_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    DETAILS = table.Column<string>(type: "NCLOB", nullable: true),
                    IP_ADDRESS = table.Column<string>(type: "NVARCHAR2(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOGS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DRIVERS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FULL_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PHONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    DRIVER_LICENCE_NUMBER = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DRIVERS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LICENCE_HOLDERS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    HOLDER_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    IDENTIFIER = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LICENCE_HOLDERS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VEHICLE_CATEGORIES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NAME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(250)", maxLength: 250, nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VEHICLE_CATEGORIES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_ROLE_CLAIMS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROLE_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    CLAIM_TYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CLAIM_VALUE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_ROLE_CLAIMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ASP_NET_ROLE_CLAIMS_ASP_NET_ROLES_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalTable: "ASP_NET_ROLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_USER_CLAIMS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    CLAIM_TYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CLAIM_VALUE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_USER_CLAIMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ASP_NET_USER_CLAIMS_ASP_NET_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "ASP_NET_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_USER_LOGINS",
                columns: table => new
                {
                    LOGIN_PROVIDER = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    PROVIDER_KEY = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    PROVIDER_DISPLAY_NAME = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_USER_LOGINS", x => new { x.LOGIN_PROVIDER, x.PROVIDER_KEY });
                    table.ForeignKey(
                        name: "FK_ASP_NET_USER_LOGINS_ASP_NET_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "ASP_NET_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_USER_PASSKEYS",
                columns: table => new
                {
                    CREDENTIAL_ID = table.Column<byte[]>(type: "RAW(1024)", maxLength: 1024, nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    DATA = table.Column<string>(type: "JSON", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_USER_PASSKEYS", x => x.CREDENTIAL_ID);
                    table.ForeignKey(
                        name: "FK_ASP_NET_USER_PASSKEYS_ASP_NET_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "ASP_NET_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_USER_ROLES",
                columns: table => new
                {
                    USER_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    ROLE_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_USER_ROLES", x => new { x.USER_ID, x.ROLE_ID });
                    table.ForeignKey(
                        name: "FK_ASP_NET_USER_ROLES_ASP_NET_ROLES_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalTable: "ASP_NET_ROLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ASP_NET_USER_ROLES_ASP_NET_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "ASP_NET_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASP_NET_USER_TOKENS",
                columns: table => new
                {
                    USER_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    LOGIN_PROVIDER = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    VALUE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASP_NET_USER_TOKENS", x => new { x.USER_ID, x.LOGIN_PROVIDER, x.NAME });
                    table.ForeignKey(
                        name: "FK_ASP_NET_USER_TOKENS_ASP_NET_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "ASP_NET_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VEHICLES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    REGISTRATION_NUMBER = table.Column<string>(type: "NVARCHAR2(15)", maxLength: 15, nullable: false),
                    REGISTRATION_KEY = table.Column<string>(type: "NVARCHAR2(15)", maxLength: 15, nullable: false),
                    MAKE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    MODEL = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    YEAR = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    VIN = table.Column<string>(type: "NVARCHAR2(17)", maxLength: 17, nullable: true),
                    CATEGORY_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LICENCE_HOLDER_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CURRENT_DRIVER_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    LICENCE_EXPIRY_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    HAS_RC1 = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    HAS_BRNC = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    HAS_PROXY_LETTER = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    HAS_PROXY_ID = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CONCURRENCY_STAMP = table.Column<string>(type: "NVARCHAR2(36)", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VEHICLES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_VEHICLES_DRIVERS_CURRENT_DRIVER_ID",
                        column: x => x.CURRENT_DRIVER_ID,
                        principalTable: "DRIVERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VEHICLES_LICENCE_HOLDERS_LICENCE_HOLDER_ID",
                        column: x => x.LICENCE_HOLDER_ID,
                        principalTable: "LICENCE_HOLDERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VEHICLES_VEHICLE_CATEGORIES_CATEGORY_ID",
                        column: x => x.CATEGORY_ID,
                        principalTable: "VEHICLE_CATEGORIES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LICENCE_RENEWALS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    VEHICLE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATE_PURCHASED = table.Column<DateTime>(type: "DATE", nullable: false),
                    PREVIOUS_EXPIRY_DATE = table.Column<DateTime>(type: "DATE", nullable: true),
                    NEW_EXPIRY_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    AMOUNT_PAID = table.Column<decimal>(type: "DECIMAL(10,2)", precision: 10, scale: 2, nullable: false),
                    REFERENCE_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    PROCESSED_BY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CREATED_BY = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LICENCE_RENEWALS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LICENCE_RENEWALS_VEHICLES_VEHICLE_ID",
                        column: x => x.VEHICLE_ID,
                        principalTable: "VEHICLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SERVICE_RECORDS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    VEHICLE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SERVICE_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    ODOMETER_KM = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WORK_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(250)", maxLength: 250, nullable: false),
                    WORKSHOP = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    COST = table.Column<decimal>(type: "DECIMAL(12,2)", precision: 12, scale: 2, nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    NEXT_SERVICE_DUE_DATE = table.Column<DateTime>(type: "DATE", nullable: true),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CREATED_BY = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SERVICE_RECORDS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SERVICE_RECORDS_VEHICLES_VEHICLE_ID",
                        column: x => x.VEHICLE_ID,
                        principalTable: "VEHICLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VEHICLE_DOCUMENTS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    VEHICLE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DOCUMENT_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    ORIGINAL_FILE_NAME = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    STORED_FILE_NAME = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    CONTENT_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    FILE_SIZE_BYTES = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    SHA256 = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    UPLOADED_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPLOADED_BY = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VEHICLE_DOCUMENTS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_VEHICLE_DOCUMENTS_VEHICLES_VEHICLE_ID",
                        column: x => x.VEHICLE_ID,
                        principalTable: "VEHICLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RENEWAL_STEPS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    LICENCE_RENEWAL_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STEP_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    STEP_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    RECORDED_BY = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RENEWAL_STEPS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RENEWAL_STEPS_LICENCE_RENEWALS_LICENCE_RENEWAL_ID",
                        column: x => x.LICENCE_RENEWAL_ID,
                        principalTable: "LICENCE_RENEWALS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "VEHICLE_CATEGORIES",
                columns: new[] { "ID", "DESCRIPTION", "IS_ACTIVE", "NAME", "SORT_ORDER" },
                values: new object[,]
                {
                    { 1, "Sedans, hatchbacks and SUVs", true, "Light passenger vehicle", 1 },
                    { 2, "Bakkies and panel vans up to 3 500 kg GVM", true, "Light delivery vehicle", 2 },
                    { 3, "Up to 16 passengers", true, "Minibus", 3 },
                    { 4, "More than 16 passengers", true, "Bus", 4 },
                    { 5, "3 500 kg to 16 000 kg GVM", true, "Medium goods vehicle", 5 },
                    { 6, "Over 16 000 kg GVM", true, "Heavy goods vehicle", 6 },
                    { 7, "Motorcycles and scooters", true, "Motorcycle", 7 },
                    { 8, "Trailers and caravans", true, "Trailer", 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ASP_NET_ROLE_CLAIMS_ROLE_ID",
                table: "ASP_NET_ROLE_CLAIMS",
                column: "ROLE_ID");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "ASP_NET_ROLES",
                column: "NORMALIZED_NAME",
                unique: true,
                filter: "\"NORMALIZED_NAME\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ASP_NET_USER_CLAIMS_USER_ID",
                table: "ASP_NET_USER_CLAIMS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ASP_NET_USER_LOGINS_USER_ID",
                table: "ASP_NET_USER_LOGINS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ASP_NET_USER_PASSKEYS_USER_ID",
                table: "ASP_NET_USER_PASSKEYS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ASP_NET_USER_ROLES_ROLE_ID",
                table: "ASP_NET_USER_ROLES",
                column: "ROLE_ID");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "ASP_NET_USERS",
                column: "NORMALIZED_EMAIL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "ASP_NET_USERS",
                column: "NORMALIZED_USER_NAME",
                unique: true,
                filter: "\"NORMALIZED_USER_NAME\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_ENTITY_TYPE_ENTITY_ID",
                table: "AUDIT_LOGS",
                columns: new[] { "ENTITY_TYPE", "ENTITY_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_TIMESTAMP_UTC",
                table: "AUDIT_LOGS",
                column: "TIMESTAMP_UTC");

            migrationBuilder.CreateIndex(
                name: "IX_LICENCE_HOLDERS_HOLDER_TYPE_IDENTIFIER",
                table: "LICENCE_HOLDERS",
                columns: new[] { "HOLDER_TYPE", "IDENTIFIER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LICENCE_RENEWALS_VEHICLE_ID_DATE_PURCHASED",
                table: "LICENCE_RENEWALS",
                columns: new[] { "VEHICLE_ID", "DATE_PURCHASED" });

            migrationBuilder.CreateIndex(
                name: "IX_RENEWAL_STEPS_LICENCE_RENEWAL_ID",
                table: "RENEWAL_STEPS",
                column: "LICENCE_RENEWAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SERVICE_RECORDS_VEHICLE_ID_SERVICE_DATE",
                table: "SERVICE_RECORDS",
                columns: new[] { "VEHICLE_ID", "SERVICE_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLE_CATEGORIES_NAME",
                table: "VEHICLE_CATEGORIES",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLE_DOCUMENTS_STORED_FILE_NAME",
                table: "VEHICLE_DOCUMENTS",
                column: "STORED_FILE_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLE_DOCUMENTS_VEHICLE_ID_DOCUMENT_TYPE",
                table: "VEHICLE_DOCUMENTS",
                columns: new[] { "VEHICLE_ID", "DOCUMENT_TYPE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLES_CATEGORY_ID",
                table: "VEHICLES",
                column: "CATEGORY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLES_CURRENT_DRIVER_ID",
                table: "VEHICLES",
                column: "CURRENT_DRIVER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLES_LICENCE_EXPIRY_DATE",
                table: "VEHICLES",
                column: "LICENCE_EXPIRY_DATE");

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLES_LICENCE_HOLDER_ID",
                table: "VEHICLES",
                column: "LICENCE_HOLDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VEHICLES_REGISTRATION_KEY",
                table: "VEHICLES",
                column: "REGISTRATION_KEY",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ASP_NET_ROLE_CLAIMS");

            migrationBuilder.DropTable(
                name: "ASP_NET_USER_CLAIMS");

            migrationBuilder.DropTable(
                name: "ASP_NET_USER_LOGINS");

            migrationBuilder.DropTable(
                name: "ASP_NET_USER_PASSKEYS");

            migrationBuilder.DropTable(
                name: "ASP_NET_USER_ROLES");

            migrationBuilder.DropTable(
                name: "ASP_NET_USER_TOKENS");

            migrationBuilder.DropTable(
                name: "AUDIT_LOGS");

            migrationBuilder.DropTable(
                name: "RENEWAL_STEPS");

            migrationBuilder.DropTable(
                name: "SERVICE_RECORDS");

            migrationBuilder.DropTable(
                name: "VEHICLE_DOCUMENTS");

            migrationBuilder.DropTable(
                name: "ASP_NET_ROLES");

            migrationBuilder.DropTable(
                name: "ASP_NET_USERS");

            migrationBuilder.DropTable(
                name: "LICENCE_RENEWALS");

            migrationBuilder.DropTable(
                name: "VEHICLES");

            migrationBuilder.DropTable(
                name: "DRIVERS");

            migrationBuilder.DropTable(
                name: "LICENCE_HOLDERS");

            migrationBuilder.DropTable(
                name: "VEHICLE_CATEGORIES");
        }
    }
}
