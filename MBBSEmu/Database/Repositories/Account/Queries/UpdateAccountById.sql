UPDATE
	Accounts
SET
	userName = @userName,
	passwordHash = @passwordHash,
	passwordSalt = @passwordSalt,
	email = @email,
	updateDate = datetime('now'),
	sex = @sex
WHERE
	accountId = @accountId