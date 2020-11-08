SELECT
	AK.*
FROM
	AccountKeys AK
INNER JOIN
	Accounts A ON
	A.accountId = AK.accountId
WHERE
	A.userName = @userName COLLATE NOCASE