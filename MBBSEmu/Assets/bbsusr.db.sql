CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL, version INTEGER NOT NULL, acs_name STRING, acs BLOB);

INSERT INTO metadata_t(record_length, physical_record_length, page_length, variable_length_records, version, acs_name, acs)
VALUES(338, 338, 1024, 0, 2, 'ALLCAPS',
x'000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f604142434445464748494a4b4c4d4e4f505152535455565758595a7b7c7d7e7f808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebfc0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff'
);

CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, null_value INTEGER NOT NULL, UNIQUE (number, segment));

INSERT INTO keys_t(number, segment, attributes, data_type, offset, length, null_value) VALUES(0, 0, 288, 11, 0, 30, 0);

CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key_0 STRING NOT NULL);

CREATE INDEX key_0_index on data_t(key_0);

CREATE TRIGGER non_modifiable BEFORE UPDATE ON data_t
  BEGIN
    SELECT
      CASE
        WHEN NEW.key_0 != OLD.key_0 THEN
          RAISE (ABORT,'You modified a non-modifiable {key.SqliteKeyName}!')
        END;
  END;
