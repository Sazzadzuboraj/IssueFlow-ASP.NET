-- IssueFlow: Project file system + issue source attachments
-- Run against your IssueFlow database if you are not applying EF migrations.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Projects') AND name = 'FolderPath'
)
BEGIN
    ALTER TABLE dbo.Projects ADD FolderPath nvarchar(200) NULL;
END
GO

IF OBJECT_ID(N'dbo.IssueAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IssueAttachments (
        Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IssueId         int NOT NULL,
        FileName        nvarchar(260) NOT NULL,
        FilePath        nvarchar(500) NOT NULL,
        ContentType     nvarchar(100) NULL,
        FileSizeBytes   bigint NOT NULL CONSTRAINT DF_IssueAttachments_Size DEFAULT(0),
        FileType        nvarchar(40) NOT NULL CONSTRAINT DF_IssueAttachments_Type DEFAULT(N'Source'),
        UploadedById    nvarchar(450) NOT NULL,
        UploadedDate    datetime2 NOT NULL CONSTRAINT DF_IssueAttachments_Date DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_IssueAttachments_Issues FOREIGN KEY (IssueId) REFERENCES dbo.Issues(Id) ON DELETE CASCADE,
        CONSTRAINT FK_IssueAttachments_Users FOREIGN KEY (UploadedById) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_IssueAttachments_IssueId ON dbo.IssueAttachments(IssueId);
END
GO
