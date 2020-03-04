UPDATE
	Accounts
SET
	userName = @userName,
	passwordHash = @passwordHash,
	passwordSalt = @passwordSalt,
	email = @email,
	updateDate = datetime('now')
WHERE
	accountId = @accountId