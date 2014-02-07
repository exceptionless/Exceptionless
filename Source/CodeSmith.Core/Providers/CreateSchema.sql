IF NOT EXISTS (SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'dbo.User') AND type = 'U')
BEGIN
	CREATE TABLE dbo.[User]
	(
		Id int identity(1,1) NOT NULL,
		EmailAddress varchar(256) NOT NULL,
		PasswordHash char(86) NOT NULL,
		PasswordSalt char(5) NOT NULL,
		Comment text NULL,
		IsApproved bit NOT NULL,
		CreatedDate datetime NOT NULL,
		LastLoginDate datetime NULL,
		LastActivityDate datetime NOT NULL,
		LastPasswordChangeDate datetime NULL,
		
		CONSTRAINT PK_User PRIMARY KEY CLUSTERED (Id ASC),
		CONSTRAINT IX_User_EmailAddress UNIQUE NONCLUSTERED (EmailAddress ASC)
	)
	
	CREATE NONCLUSTERED INDEX IX_User_LastActivityDate ON dbo.[User]
	(
		LastActivityDate ASC
	)
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'dbo.Role') AND type = 'U')
BEGIN
	CREATE TABLE dbo.Role
	(
		Id int identity(1,1) NOT NULL,
		RoleName varchar(256) NOT NULL,
		
		CONSTRAINT PK_Role PRIMARY KEY CLUSTERED (Id ASC),
		CONSTRAINT IX_Role_RoleName UNIQUE NONCLUSTERED (RoleName ASC)
	)
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'dbo.UserRole') AND type = 'U')
BEGIN
	CREATE TABLE dbo.UserRole
	(
		Id int IDENTITY(1,1) NOT NULL,
		RoleId int NOT NULL,
		UserId int NOT NULL,
		
		CONSTRAINT PK_UserRole PRIMARY KEY CLUSTERED (Id ASC),
		CONSTRAINT FK_UserRole_Role FOREIGN KEY (RoleId) REFERENCES dbo.Role (Id) ON UPDATE CASCADE ON DELETE CASCADE,
		CONSTRAINT FK_UserRole_User FOREIGN KEY (UserId) REFERENCES dbo.[User] (Id) ON UPDATE CASCADE ON DELETE CASCADE,
		CONSTRAINT IX_UserRole_UserId_RoleId UNIQUE NONCLUSTERED (UserId ASC, RoleId ASC)
	)
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'dbo.UserProfile') AND type = 'U')
BEGIN
	CREATE TABLE dbo.UserProfile
	(
		Id int IDENTITY(1,1) NOT NULL,
		UserId int NOT NULL,
		-- Begin Profile Fields --
		FullName varchar(255) NULL,
		BirthDate datetime NULL,
		PageSize int NULL,
		-- End Profile Fields --
		LastUpdateDate datetime NOT NULL,
		
		CONSTRAINT PK_UserProfile PRIMARY KEY CLUSTERED (Id ASC),
		CONSTRAINT FK_UserProfile_User FOREIGN KEY (UserId) REFERENCES dbo.[User] (Id) ON UPDATE CASCADE ON DELETE CASCADE,
		CONSTRAINT IX_UserProfile_UserId UNIQUE NONCLUSTERED (UserId ASC)
	)
END