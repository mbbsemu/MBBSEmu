INSERT INTO Accounts (
	userName,
	passwordHash,
	passwordSalt,
	email,
	createDate,
	updateDate)
VALUES (
	@userName,
	@passwordHash,
	@passwordSalt,
	@email,
	datetime('now'),
	datetime('now')
);

SELECT last_row_id();