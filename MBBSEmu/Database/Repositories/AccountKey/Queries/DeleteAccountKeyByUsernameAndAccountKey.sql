DELETE FROM
	AccountKeys
WHERE
	accountId IN (SELECT accountId FROM Accounts WHERE userName = @userName)
	AND accountKey = @accountKey