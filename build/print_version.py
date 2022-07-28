import sys

import version

build = sys.argv[1]
is_final_build = sys.argv[2] == "true"
as_pypi_version = sys.argv[3] == "true"

if is_final_build:
    build = None

print(version.get_version(build, as_pypi_version))
