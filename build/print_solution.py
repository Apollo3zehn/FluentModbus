import json

with open("solution.json", "r") as fh:
    solution_data = json.load(fh)
        
print(
f"""
AUTHORS={solution_data["authors"]}
COMPANY={solution_data["company"]}
COPYRIGHT={solution_data["copyright"]}
PRODUCT={solution_data["product"]}
LICENSE={solution_data["license"]}
PROJECT_URL={solution_data["project-url"]}
REPOSITORY_URL={solution_data["repository-url"]}
ICON_URL={solution_data["icon-url"]}
""")