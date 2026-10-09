CREATE TABLE "Activities" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Activities" PRIMARY KEY AUTOINCREMENT,
    "Direction" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "EmployeeId" INTEGER NOT NULL,
    "Deadline" TEXT NOT NULL,
    "Progress" INTEGER NOT NULL,
    CONSTRAINT "FK_Activities_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Attachments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Attachments" PRIMARY KEY AUTOINCREMENT,
    "Resource" TEXT NOT NULL,
    "ResourceId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "ContentType" TEXT NOT NULL,
    "Content" BLOB NOT NULL
);

CREATE TABLE "AttendanceRecords" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AttendanceRecords" PRIMARY KEY AUTOINCREMENT,
    "TrainingSessionId" INTEGER NOT NULL,
    "EmployeeId" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "TestResult" INTEGER NOT NULL,
    "TestScore" TEXT NULL,
    "Source" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AttendanceRecords_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AttendanceRecords_TrainingSessions_TrainingSessionId" FOREIGN KEY ("TrainingSessionId") REFERENCES "TrainingSessions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AuditEvents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AuditEvents" PRIMARY KEY AUTOINCREMENT,
    "At" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Details" TEXT NOT NULL
);

CREATE TABLE "Contractors" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Contractors" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "License" TEXT NOT NULL,
    "ValidUntil" TEXT NOT NULL,
    "WasteClasses" TEXT NOT NULL
);

CREATE TABLE "Disposals" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Disposals" PRIMARY KEY AUTOINCREMENT,
    "WriteOffActId" INTEGER NOT NULL,
    "ContractorId" INTEGER NOT NULL,
    "ContractNumber" TEXT NOT NULL,
    "TransferDocument" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    CONSTRAINT "FK_Disposals_Contractors_ContractorId" FOREIGN KEY ("ContractorId") REFERENCES "Contractors" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Disposals_WriteOffActs_WriteOffActId" FOREIGN KEY ("WriteOffActId") REFERENCES "WriteOffActs" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Employees" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Employees" PRIMARY KEY AUTOINCREMENT,
    "PersonnelNumber" TEXT NOT NULL,
    "Department" TEXT NOT NULL,
    "GasMaskSize" INTEGER NOT NULL,
    "SuitSize" INTEGER NOT NULL,
    "Shift" INTEGER NOT NULL,
    "IsSubjectToEvacuation" INTEGER NOT NULL,
    "NeedsAssistance" INTEGER NOT NULL,
    "Reserved" INTEGER NOT NULL,
    "UmcTrained" INTEGER NOT NULL,
    "Phone" TEXT NOT NULL,
    "NfgoRole" TEXT NOT NULL,
    "NfgoUnitId" INTEGER NULL,
    "ProtectiveStructureId" INTEGER NULL,
    "VehicleId" INTEGER NULL,
    "ReceptionPointId" INTEGER NULL,
    "FullName" TEXT NOT NULL,
    "Position" TEXT NULL,
    "PersonnelStatus" INTEGER NOT NULL,
    "TrainingGroupId" INTEGER NULL,
    CONSTRAINT "FK_Employees_NfgoUnits_NfgoUnitId" FOREIGN KEY ("NfgoUnitId") REFERENCES "NfgoUnits" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Employees_ProtectiveStructures_ProtectiveStructureId" FOREIGN KEY ("ProtectiveStructureId") REFERENCES "ProtectiveStructures" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Employees_ReceptionPoints_ReceptionPointId" FOREIGN KEY ("ReceptionPointId") REFERENCES "ReceptionPoints" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Employees_TrainingGroups_TrainingGroupId" FOREIGN KEY ("TrainingGroupId") REFERENCES "TrainingGroups" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Employees_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EquipmentNorms" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_EquipmentNorms" PRIMARY KEY AUTOINCREMENT,
    "NfgoUnitId" INTEGER NOT NULL,
    "SizTypeId" INTEGER NOT NULL,
    "Quantity" INTEGER NOT NULL,
    CONSTRAINT "FK_EquipmentNorms_NfgoUnits_NfgoUnitId" FOREIGN KEY ("NfgoUnitId") REFERENCES "NfgoUnits" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EquipmentNorms_SizTypes_SizTypeId" FOREIGN KEY ("SizTypeId") REFERENCES "SizTypes" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EvacuationRoutes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_EvacuationRoutes" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "ReceptionPointId" INTEGER NOT NULL,
    "Reserve" INTEGER NOT NULL,
    "StartPoint" TEXT NOT NULL,
    "DistanceKm" REAL NOT NULL,
    "TravelTimeMinutes" INTEGER NOT NULL,
    "Description" TEXT NOT NULL,
    CONSTRAINT "FK_EvacuationRoutes_ReceptionPoints_ReceptionPointId" FOREIGN KEY ("ReceptionPointId") REFERENCES "ReceptionPoints" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ExternalIdentities" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ExternalIdentities" PRIMARY KEY AUTOINCREMENT,
    "Source" TEXT NOT NULL,
    "SourceId" TEXT NOT NULL,
    "EmployeeId" INTEGER NOT NULL,
    CONSTRAINT "FK_ExternalIdentities_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Formations" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Formations" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "MchsCode" TEXT NOT NULL,
    "Location" TEXT NOT NULL,
    "Purpose" TEXT NOT NULL
);

CREATE TABLE "Inspections" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Inspections" PRIMARY KEY AUTOINCREMENT,
    "ProtectiveStructureId" INTEGER NOT NULL,
    "Date" TEXT NOT NULL,
    "Inspector" TEXT NOT NULL,
    "Condition" TEXT NOT NULL,
    "VentilationWorks" INTEGER NOT NULL,
    "DoorsSealed" INTEGER NOT NULL,
    "Findings" TEXT NOT NULL,
    "NextInspectionDate" TEXT NOT NULL,
    CONSTRAINT "FK_Inspections_ProtectiveStructures_ProtectiveStructureId" FOREIGN KEY ("ProtectiveStructureId") REFERENCES "ProtectiveStructures" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "IssueLines" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IssueLines" PRIMARY KEY AUTOINCREMENT,
    "IssueStatementId" INTEGER NOT NULL,
    "EmployeeId" INTEGER NOT NULL,
    "SizCardId" INTEGER NOT NULL,
    "EmployeeName" TEXT NOT NULL,
    "PersonnelNumber" TEXT NOT NULL,
    "ItemName" TEXT NOT NULL,
    "BatchNumber" TEXT NOT NULL,
    "Size" TEXT NOT NULL,
    CONSTRAINT "FK_IssueLines_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IssueLines_IssueStatements_IssueStatementId" FOREIGN KEY ("IssueStatementId") REFERENCES "IssueStatements" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IssueLines_SizCards_SizCardId" FOREIGN KEY ("SizCardId") REFERENCES "SizCards" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "IssueStatements" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IssueStatements" PRIMARY KEY AUTOINCREMENT,
    "Number" TEXT NOT NULL,
    "Department" TEXT NOT NULL,
    "Basis" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "SignedBy" TEXT NULL
);

CREATE TABLE "Leaves" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Leaves" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NOT NULL,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    CONSTRAINT "FK_Leaves_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "NfgoOrders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_NfgoOrders" PRIMARY KEY AUTOINCREMENT,
    "Number" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "Organization" TEXT NOT NULL,
    "Basis" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Snapshot" TEXT NOT NULL,
    "SignedBy" TEXT NULL
);

CREATE TABLE "NfgoUnits" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_NfgoUnits" PRIMARY KEY AUTOINCREMENT,
    "FormationId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Capacity" INTEGER NOT NULL,
    "Location" TEXT NOT NULL,
    CONSTRAINT "FK_NfgoUnits_Formations_FormationId" FOREIGN KEY ("FormationId") REFERENCES "Formations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Notifications" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NULL,
    "Message" TEXT NOT NULL,
    "Channel" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "Acknowledged" INTEGER NOT NULL,
    CONSTRAINT "FK_Notifications_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Organizations" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Organizations" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Head" TEXT NOT NULL
);

CREATE TABLE "ProtectiveStructures" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ProtectiveStructures" PRIMARY KEY AUTOINCREMENT,
    "RegistrationNumber" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "ProtectionClass" TEXT NOT NULL,
    "Address" TEXT NOT NULL,
    "Capacity" INTEGER NOT NULL,
    "Condition" TEXT NOT NULL,
    "ResponsiblePerson" TEXT NOT NULL,
    "NextInspectionDate" TEXT NULL
);

CREATE TABLE "ReceptionPoints" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReceptionPoints" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Address" TEXT NOT NULL,
    "Capacity" INTEGER NOT NULL,
    "ContactPerson" TEXT NOT NULL,
    "ContactPhone" TEXT NOT NULL
);

CREATE TABLE "SizBatches" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SizBatches" PRIMARY KEY AUTOINCREMENT,
    "SizTypeId" INTEGER NOT NULL,
    "WarehouseId" INTEGER NOT NULL,
    "BatchNumber" TEXT NOT NULL,
    "Manufacturer" TEXT NOT NULL,
    "Size" TEXT NOT NULL,
    "Shelf" TEXT NOT NULL,
    "ManufactureDate" TEXT NOT NULL,
    "ReceivedDate" TEXT NOT NULL,
    "ExpiryDate" TEXT NOT NULL,
    "LastInspectionDate" TEXT NULL,
    "InspectionNumber" TEXT NOT NULL,
    "Certificate" TEXT NOT NULL,
    CONSTRAINT "FK_SizBatches_SizTypes_SizTypeId" FOREIGN KEY ("SizTypeId") REFERENCES "SizTypes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_SizBatches_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SizCards" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SizCards" PRIMARY KEY AUTOINCREMENT,
    "SizBatchId" INTEGER NOT NULL,
    "InventoryNumber" TEXT NOT NULL,
    "SerialNumber" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Condition" TEXT NOT NULL,
    "EmployeeId" INTEGER NULL,
    "NfgoUnitId" INTEGER NULL,
    "IssueDate" TEXT NULL,
    CONSTRAINT "FK_SizCards_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_SizCards_NfgoUnits_NfgoUnitId" FOREIGN KEY ("NfgoUnitId") REFERENCES "NfgoUnits" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_SizCards_SizBatches_SizBatchId" FOREIGN KEY ("SizBatchId") REFERENCES "SizBatches" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SizTypes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SizTypes" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "UnitPrice" TEXT NOT NULL,
    "DeliveryDays" INTEGER NOT NULL,
    "ShelfLifeYears" INTEGER NOT NULL,
    "PerEmployee" INTEGER NOT NULL,
    "WasteClass" TEXT NOT NULL
);

CREATE TABLE "SystemOperationalStates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SystemOperationalStates" PRIMARY KEY AUTOINCREMENT,
    "Mode" INTEGER NOT NULL,
    "ActiveOrderId" INTEGER NULL,
    "ActivatedAt" TEXT NULL,
    "SpecialWorkScheduleEnabled" INTEGER NOT NULL,
    "LeaveRestrictionsEnabled" INTEGER NOT NULL,
    "EmergencyNotificationsEnabled" INTEGER NOT NULL,
    "ProtectedFormsEnabled" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_SystemOperationalStates_WartimeTransitionOrders_ActiveOrderId" FOREIGN KEY ("ActiveOrderId") REFERENCES "WartimeTransitionOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TrainingAuditLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TrainingAuditLogs" PRIMARY KEY AUTOINCREMENT,
    "TrainingSessionId" INTEGER NULL,
    "EmployeeId" INTEGER NULL,
    "Action" TEXT NOT NULL,
    "Details" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE "TrainingChangeRequests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TrainingChangeRequests" PRIMARY KEY AUTOINCREMENT,
    "TrainingSessionId" INTEGER NOT NULL,
    "RequestedBy" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ResolvedBy" TEXT NULL,
    "ResolvedAt" TEXT NULL,
    CONSTRAINT "FK_TrainingChangeRequests_TrainingSessions_TrainingSessionId" FOREIGN KEY ("TrainingSessionId") REFERENCES "TrainingSessions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TrainingGroups" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TrainingGroups" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Year" INTEGER NOT NULL,
    "LeaderName" TEXT NOT NULL,
    "LeaderPosition" TEXT NULL
);

CREATE TABLE "TrainingSessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TrainingSessions" PRIMARY KEY AUTOINCREMENT,
    "TrainingGroupId" INTEGER NOT NULL,
    "TopicNumber" INTEGER NOT NULL,
    "TopicName" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "StartTime" TEXT NULL,
    "Hours" INTEGER NOT NULL,
    "ClassType" TEXT NOT NULL,
    "Location" TEXT NULL,
    "LeaderName" TEXT NOT NULL,
    "IsSigned" INTEGER NOT NULL,
    "SignedBy" TEXT NULL,
    "SignedAt" TEXT NULL,
    CONSTRAINT "FK_TrainingSessions_TrainingGroups_TrainingGroupId" FOREIGN KEY ("TrainingGroupId") REFERENCES "TrainingGroups" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Vehicles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Vehicles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "PlateNumber" TEXT NOT NULL,
    "Capacity" INTEGER NOT NULL,
    "Contracted" INTEGER NOT NULL,
    "ContractValidUntil" TEXT NULL,
    "ContractNumber" TEXT NOT NULL,
    "DriverName" TEXT NOT NULL,
    "Ready" INTEGER NOT NULL
);

CREATE TABLE "Warehouses" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Warehouses" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Address" TEXT NOT NULL
);

CREATE TABLE "WartimeActionLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WartimeActionLogs" PRIMARY KEY AUTOINCREMENT,
    "WartimeTransitionOrderId" INTEGER NOT NULL,
    "Action" TEXT NOT NULL,
    "Details" TEXT NULL,
    "ExecutedAt" TEXT NOT NULL,
    CONSTRAINT "FK_WartimeActionLogs_WartimeTransitionOrders_WartimeTransitionOrderId" FOREIGN KEY ("WartimeTransitionOrderId") REFERENCES "WartimeTransitionOrders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "WartimeTransitionOrders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WartimeTransitionOrders" PRIMARY KEY AUTOINCREMENT,
    "OrderNumber" TEXT NOT NULL,
    "OrganizationName" TEXT NOT NULL,
    "OrderDate" TEXT NOT NULL,
    "EffectiveFrom" TEXT NOT NULL,
    "Basis" TEXT NOT NULL,
    "ResponsiblePerson" TEXT NOT NULL,
    "ResponsiblePosition" TEXT NULL,
    "NotificationProcedure" TEXT NULL,
    "WorkScheduleDescription" TEXT NULL,
    "AdditionalInstructions" TEXT NULL,
    "CreatedBy" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "SignedBy" TEXT NULL,
    "SignedAt" TEXT NULL,
    "ActivatedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE "WorkSchedules" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkSchedules" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NOT NULL,
    "Mode" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    CONSTRAINT "FK_WorkSchedules_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WriteOffActs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WriteOffActs" PRIMARY KEY AUTOINCREMENT,
    "Number" TEXT NOT NULL,
    "Commission" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "SignedBy" TEXT NULL
);

CREATE TABLE "WriteOffLines" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WriteOffLines" PRIMARY KEY AUTOINCREMENT,
    "WriteOffActId" INTEGER NOT NULL,
    "SizCardId" INTEGER NOT NULL,
    CONSTRAINT "FK_WriteOffLines_SizCards_SizCardId" FOREIGN KEY ("SizCardId") REFERENCES "SizCards" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WriteOffLines_WriteOffActs_WriteOffActId" FOREIGN KEY ("WriteOffActId") REFERENCES "WriteOffActs" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Activities_EmployeeId" ON "Activities" ("EmployeeId");

CREATE INDEX "IX_AttendanceRecords_EmployeeId" ON "AttendanceRecords" ("EmployeeId");

CREATE UNIQUE INDEX "IX_AttendanceRecords_TrainingSessionId_EmployeeId" ON "AttendanceRecords" ("TrainingSessionId", "EmployeeId");

CREATE INDEX "IX_Disposals_ContractorId" ON "Disposals" ("ContractorId");

CREATE UNIQUE INDEX "IX_Disposals_WriteOffActId" ON "Disposals" ("WriteOffActId");

CREATE INDEX "IX_Employees_NfgoUnitId" ON "Employees" ("NfgoUnitId");

CREATE UNIQUE INDEX "IX_Employees_PersonnelNumber" ON "Employees" ("PersonnelNumber");

CREATE INDEX "IX_Employees_ProtectiveStructureId" ON "Employees" ("ProtectiveStructureId");

CREATE INDEX "IX_Employees_ReceptionPointId" ON "Employees" ("ReceptionPointId");

CREATE INDEX "IX_Employees_TrainingGroupId" ON "Employees" ("TrainingGroupId");

CREATE INDEX "IX_Employees_VehicleId" ON "Employees" ("VehicleId");

CREATE UNIQUE INDEX "IX_EquipmentNorms_NfgoUnitId_SizTypeId" ON "EquipmentNorms" ("NfgoUnitId", "SizTypeId");

CREATE INDEX "IX_EquipmentNorms_SizTypeId" ON "EquipmentNorms" ("SizTypeId");

CREATE INDEX "IX_EvacuationRoutes_ReceptionPointId" ON "EvacuationRoutes" ("ReceptionPointId");

CREATE INDEX "IX_ExternalIdentities_EmployeeId" ON "ExternalIdentities" ("EmployeeId");

CREATE UNIQUE INDEX "IX_ExternalIdentities_Source_SourceId" ON "ExternalIdentities" ("Source", "SourceId");

CREATE INDEX "IX_Inspections_ProtectiveStructureId" ON "Inspections" ("ProtectiveStructureId");

CREATE INDEX "IX_IssueLines_EmployeeId" ON "IssueLines" ("EmployeeId");

CREATE UNIQUE INDEX "IX_IssueLines_IssueStatementId_SizCardId" ON "IssueLines" ("IssueStatementId", "SizCardId");

CREATE INDEX "IX_IssueLines_SizCardId" ON "IssueLines" ("SizCardId");

CREATE UNIQUE INDEX "IX_IssueStatements_Number" ON "IssueStatements" ("Number");

CREATE INDEX "IX_Leaves_EmployeeId" ON "Leaves" ("EmployeeId");

CREATE UNIQUE INDEX "IX_NfgoOrders_Number" ON "NfgoOrders" ("Number");

CREATE INDEX "IX_NfgoUnits_FormationId" ON "NfgoUnits" ("FormationId");

CREATE INDEX "IX_Notifications_EmployeeId" ON "Notifications" ("EmployeeId");

CREATE UNIQUE INDEX "IX_ProtectiveStructures_RegistrationNumber" ON "ProtectiveStructures" ("RegistrationNumber");

CREATE UNIQUE INDEX "IX_SizBatches_SizTypeId_BatchNumber_Size" ON "SizBatches" ("SizTypeId", "BatchNumber", "Size");

CREATE INDEX "IX_SizBatches_WarehouseId" ON "SizBatches" ("WarehouseId");

CREATE INDEX "IX_SizCards_EmployeeId" ON "SizCards" ("EmployeeId");

CREATE UNIQUE INDEX "IX_SizCards_InventoryNumber" ON "SizCards" ("InventoryNumber");

CREATE INDEX "IX_SizCards_NfgoUnitId" ON "SizCards" ("NfgoUnitId");

CREATE INDEX "IX_SizCards_SizBatchId" ON "SizCards" ("SizBatchId");

CREATE INDEX "IX_SystemOperationalStates_ActiveOrderId" ON "SystemOperationalStates" ("ActiveOrderId");

CREATE INDEX "IX_TrainingChangeRequests_TrainingSessionId" ON "TrainingChangeRequests" ("TrainingSessionId");

CREATE UNIQUE INDEX "IX_TrainingGroups_Year_Name" ON "TrainingGroups" ("Year", "Name");

CREATE INDEX "IX_TrainingSessions_TrainingGroupId" ON "TrainingSessions" ("TrainingGroupId");

CREATE INDEX "IX_WartimeActionLogs_WartimeTransitionOrderId" ON "WartimeActionLogs" ("WartimeTransitionOrderId");

CREATE UNIQUE INDEX "IX_WartimeTransitionOrders_OrderNumber" ON "WartimeTransitionOrders" ("OrderNumber");

CREATE INDEX "IX_WorkSchedules_EmployeeId" ON "WorkSchedules" ("EmployeeId");

CREATE UNIQUE INDEX "IX_WriteOffActs_Number" ON "WriteOffActs" ("Number");

CREATE INDEX "IX_WriteOffLines_SizCardId" ON "WriteOffLines" ("SizCardId");

CREATE UNIQUE INDEX "IX_WriteOffLines_WriteOffActId_SizCardId" ON "WriteOffLines" ("WriteOffActId", "SizCardId");
