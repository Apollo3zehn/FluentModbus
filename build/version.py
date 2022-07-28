import json
from typing import Optional


def get_version(build: Optional[str], as_pypi_version: bool = False) -> str:

    with open("version.json", "r") as fh:
        version_data = json.load(fh)
        
    # version
    version = version_data["version"]
    suffix = version_data["suffix"]

    if suffix:
        version = f"{version}-{suffix}"

        if build:

            # PEP440 does not support SemVer versioning (https://semver.org/#spec-item-9)
            if as_pypi_version:
                version = f"{version}{int(build):03d}"

            else:
                version = f"{version}.{build}"

    return version
