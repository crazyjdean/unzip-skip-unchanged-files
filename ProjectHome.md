A unzip tool written in C#, which checks MD5 hash before unzipping each file.
---
If the hash matches previous version, we skip the extraction of this file.
---
Hashes are stored in NTFS Alternate files streams (on Linux, one should use xattr API, but this is not implemented here)