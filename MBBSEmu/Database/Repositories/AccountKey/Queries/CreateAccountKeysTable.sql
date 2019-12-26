CREATE TABLE AccountKeys (
	accountKeyId INTEGER PRIMARY KEY,
	accountId INTEGER NOT NULL,
	accountKey TEXT NOT NULL,
	createDate TEXT NOT NULL,
	updateDate TEXT NOT NULL
);