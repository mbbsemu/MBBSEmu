INSERT INTO AccountKeys (
	accountId,
	accountKey,
	createDate,
	updateDate)
SELECT 
	A.accountID, 
	@accountKey, 
	datetime('now'), 
	datetime('now') 
FROM 
	Accounts A 
WHERE 
	userName = @userName COLLATE NOCASE;

SELECT last_insert_rowid();