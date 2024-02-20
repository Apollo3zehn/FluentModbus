import sys

import version

build = sys.argv[1]
is_final_build = sys.argv[2] == "true"
version_type = sys.argv[3]

if is_final_build:
    build = None

print(version.get_version(build, version_type))
