CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `AlertEvents` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` varchar(50) NOT NULL,
    `TriggerClass` int NOT NULL,
    `SensorConfidence` float NOT NULL,
    `VisionConfidence` float NULL,
    `FcmSent` tinyint(1) NOT NULL DEFAULT FALSE,
    `Message` varchar(255) NOT NULL,
    `TimestampUtc` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
);

CREATE TABLE `DeviceStatuses` (
    `DeviceId` varchar(50) NOT NULL,
    `IsOnline` tinyint(1) NOT NULL DEFAULT FALSE,
    `LastRiskClass` int NOT NULL,
    `LastSeenUtc` datetime(6) NOT NULL,
    `IpAddress` varchar(45) NULL,
    `FirmwareVersion` varchar(30) NOT NULL DEFAULT '1.0.0',
    PRIMARY KEY (`DeviceId`)
);

CREATE TABLE `TelemetryLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` varchar(50) NOT NULL,
    `Smoke` float NOT NULL,
    `Gas` float NOT NULL,
    `Co` float NOT NULL,
    `Temperature` float NOT NULL,
    `RiskClass` int NOT NULL,
    `TimestampUtc` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
);

CREATE INDEX `IX_AlertEvents_DeviceId_TimestampUtc` ON `AlertEvents` (`DeviceId`, `TimestampUtc`);

CREATE INDEX `IX_AlertEvents_TriggerClass` ON `AlertEvents` (`TriggerClass`);

CREATE INDEX `IX_TelemetryLogs_DeviceId_TimestampUtc` ON `TelemetryLogs` (`DeviceId`, `TimestampUtc`);

CREATE INDEX `IX_TelemetryLogs_TimestampUtc` ON `TelemetryLogs` (`TimestampUtc`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261015043315_InitialCreate', '10.0.13');

COMMIT;

