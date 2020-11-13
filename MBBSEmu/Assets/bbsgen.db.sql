CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL, version INTEGER NOT NULL);

INSERT INTO metadata_t(record_length, physical_record_length, page_length, variable_length_records, version) VALUES(55, 75, 1024, 1, 1);

CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, null_value INTEGER NOT NULL, UNIQUE (number, segment));

INSERT INTO keys_t(number, segment, attributes, data_type, offset, length, null_value) VALUES(0, 0, 307, 11, 0, 30, 0);
INSERT INTO keys_t(number, segment, attributes, data_type, offset, length, null_value) VALUES(0, 1, 291, 11, 30, 25, 0);

INSERT INTO keys_t(number, segment, attributes, data_type, offset, length, null_value) VALUES(1, 0, 291, 11, 30, 25, 0);

CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key_0 STRING NOT NULL, key_1 STRING NOT NULL);

CREATE INDEX key_0_index on data_t(key_0);
CREATE INDEX key_1_index on data_t(key_1);
