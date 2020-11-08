SELECT
	*
FROM
	Accounts
WHERE
	userName = @userName COLLATE NOCASE;