SELECT
	*
FROM
	sqlite_master
WHERE
	type = 'table'
	and name = 'AccountKeys';