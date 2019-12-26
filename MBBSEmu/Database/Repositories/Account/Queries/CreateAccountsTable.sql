CREATE TABLE Accounts (
	userId INTEGER PRIMARY KEY,
	userName TEXT NOT NULL UNIQUE,
	passwordHash TEXT NOT NULL,
	passwordSalt TEXT NOT NULL,
	email TEXT NOT NULL UNIQUE,
	userKey TEXT NOT NULL,
	createDate TEXT NOT NULL,
	updateDate TEXT NOT NULL
);