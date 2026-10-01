-- Local dev DB only (DancingGoat example). Never run on a real project.
-- Back-dates ContentItemLanguageMetadataModifiedWhen on some language variants so the
-- "Content age", "Oldest content", "Unused reusable items" and "Action needed" tiles of the
-- content inventory report have data. Re-runnable: dates are relative to now.
--
-- Run from Git Bash:
--   docker cp .agent-resources/seed-content-age.sql mssql2022:/tmp/seed-content-age.sql
--   MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -i /tmp/seed-content-age.sql

SET NOCOUNT ON;

DECLARE @Now datetime2 = SYSDATETIME();

-- Spread by item ID: some variants over 12 months, 6-12 months and 3-6 months old.
UPDATE M
SET M.[ContentItemLanguageMetadataModifiedWhen] =
    CASE
        WHEN I.[ContentItemID] % 10 = 1 THEN DATEADD(day, -(400 + I.[ContentItemID]), @Now)
        WHEN I.[ContentItemID] % 10 = 2 THEN DATEADD(day, -(220 + I.[ContentItemID]), @Now)
        WHEN I.[ContentItemID] % 10 = 3 THEN DATEADD(day, -(120 + I.[ContentItemID] % 50), @Now)
        ELSE M.[ContentItemLanguageMetadataModifiedWhen]
    END
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
WHERE I.[ContentItemID] % 10 IN (1, 2, 3)
    AND M.[ContentItemLanguageMetadataContentWorkflowStepID] IS NULL;

-- Variants in a workflow step: waiting 30 days (over the 14 day threshold).
UPDATE [CMS_ContentItemLanguageMetadata]
SET [ContentItemLanguageMetadataModifiedWhen] = DATEADD(day, -30, @Now)
WHERE [ContentItemLanguageMetadataContentWorkflowStepID] IS NOT NULL;

SELECT
    SUM(CASE WHEN [ContentItemLanguageMetadataModifiedWhen] < DATEADD(month, -12, @Now) THEN 1 ELSE 0 END) AS [Over12Months],
    SUM(CASE WHEN [ContentItemLanguageMetadataModifiedWhen] < DATEADD(month, -6, @Now)
        AND [ContentItemLanguageMetadataModifiedWhen] >= DATEADD(month, -12, @Now) THEN 1 ELSE 0 END) AS [Months6To12],
    SUM(CASE WHEN [ContentItemLanguageMetadataModifiedWhen] < DATEADD(month, -3, @Now)
        AND [ContentItemLanguageMetadataModifiedWhen] >= DATEADD(month, -6, @Now) THEN 1 ELSE 0 END) AS [Months3To6]
FROM [CMS_ContentItemLanguageMetadata];
