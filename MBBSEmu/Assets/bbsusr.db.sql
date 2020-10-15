CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL);

INSERT INTO metadata_t(record_length, physical_record_length, page_length) VALUES(338, 338, 1024);

CREATE TABLE keys_t(id INTEGER PRIMARY KEY, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL);

INSERT INTO keys_t(id, attributes, data_type, offset, length) VALUES(0, 0, 11, 0, 30);

CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key0 STRING NOT NULL);

