CREATE TABLE Accounts (
	userId INTEGER PRIMARY KEY,
	userName TEXT NOT NULL,
	passwordHash TEXT NOT NULL,
	passwordSalt TEXT NOT NULL,
	email TEXT NOT NULL,
	createDate TEXT NOT NULL
);