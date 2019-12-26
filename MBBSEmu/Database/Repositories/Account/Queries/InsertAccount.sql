INSERT INTO Accounts (
	userName,
	passwordHash,
	passwordSalt,
	email,
	userKey,
	createDate,
	updateDate)
VALUES (
	@userName,
	@passwordHash,
	@passwordSalt,
	@email,
	'DEMO',
	datetime('now'),
	datetime('now')
);