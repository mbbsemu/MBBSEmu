INSERT INTO Accounts (
	userName,
	passwordHash,
	passwordSalt,
	email,
	createDate,
	updateDate,
	sex)
VALUES (
	@userName,
	@passwordHash,
	@passwordSalt,
	@email,
	datetime('now'),
	datetime('now'),
	@sex
);

SELECT last_insert_rowid();