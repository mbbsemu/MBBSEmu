INSERT INTO AccountKeys (
	accountId,
	accountKey,
	createDate,
	updateDate)
VALUES (
	@accountId,
	@accountKey,
	datetime('now'),
	datetime('now')
);

SELECT last_insert_rowid();