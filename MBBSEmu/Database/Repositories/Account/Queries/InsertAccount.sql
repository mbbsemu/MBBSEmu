INSERT INTO Accounts (
	userName,
	passwordHash,
	passwordSalt,
	email,
	createDate)
VALUES (
	@userName,
	@passwordHash,
	@passwordSalt,
	@email,
	datetime('now')
);