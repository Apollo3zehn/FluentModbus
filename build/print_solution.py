import json

with open("solution.json", "r") as fh:
    solution_data = json.load(fh)
        
print(
f"""
AUTHORS="{solution_data["authors"]}"\n
COMPANY="{solution_data["company"]}"\n
COPYRIGHT="{solution_data["copyright"]}"\n
PRODUCT="{solution_data["product"]}"\n
LICENSE="{solution_data["license"]}"\n
PROJECT_URL="{solution_data["project-url"]}"\n
REPOSITORY_URL="{solution_data["repository-url"]}"\n
ICON_URL="{solution_data["icon-url"]}"\n
""")