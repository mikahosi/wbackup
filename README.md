wbackup
===============

1. Calclate 32bit checksum and SHA256 hash.
* this file exist in duplicate check database.
* if not exist then comppress source file and export binary database.
* add sourcefile database.
* remove oldest files.
* remove archive flags.

## duplicate check database
* 32bit checksum
* SHA-256 hash
* backup count (64bit)
* binary stream id (64bit)

## binary data stream
* backup count (64bit)
* binary stream id (64bit)
* 32bit HEX
