# Contributing to mes-common-library

First off, thanks for taking the time to contribute! ❤️

All types of contributions are encouraged and valued. See the [Table of Contents](#table-of-contents) for different ways to help and details about how this project handles them. Please make sure to read the relevant section before making your contribution. It will make it a lot easier for us maintainers and smooth out the experience for all involved. The community looks forward to your contributions. 🎉

## Table of Contents

- [Contributing to mes-common-library](#contributing-to-mes-common-library)
  - [Table of Contents](#table-of-contents)
  - [I Have a Question](#i-have-a-question)
  - [Feature Lifecycle](#feature-lifecycle)
    - [1. Identify](#1-identify)
    - [2. Approval](#2-approval)
    - [3. Implementation / Migration](#3-implementation--migration)
      - [Ground rules](#ground-rules)
    - [4. Internal Demo](#4-internal-demo)
    - [5. Release and Publish](#5-release-and-publish)
    - [6. Global Demo](#6-global-demo)
  - [I Want To Contribute](#i-want-to-contribute)
    - [Legal Notice](#legal-notice)
    - [Reporting Bugs](#reporting-bugs)
      - [Before Submitting a Bug Report](#before-submitting-a-bug-report)
      - [How Do I Submit a Good Bug Report?](#how-do-i-submit-a-good-bug-report)
    - [Your First Code Contribution](#your-first-code-contribution)
    - [Commit Message \& Hook Enforcement](#commit-message--hook-enforcement)



## I Have a Question

> If you want to ask a question, we assume that you have read this and the [README](https://criticalmanufacturing.github.io/mes-common-library) file as well.

It is best to search for existing [Issues](https://github.com/criticalmanufacturing/mes-common-library/issues) that might help you. In case you have found a suitable issue and still need clarification, you can write your question at the issue's comments section.

If you then still feel the need to ask a question and need clarification, we recommend the following:

- Open an [Issue](https://github.com/criticalmanufacturing/mes-common-library/issues/new).
- Provide as much context as you can about what you're running into.
- Provide project and platform versions (nodejs, npm, etc), depending on what seems relevant.

We will then take care of the issue as soon as possible.

## Feature Lifecycle

New features proposed for the Common Library — whether built from scratch or migrated from an existing customization project/template — must go through the following lifecycle before they become part of the repository. Project Teams and Portfolio Management can identify candidates at any time; the feature owner assigned to the Issue is responsible for steering the feature through the remaining phases.

### 1. Identify

**Who:** Project Teams (Engineers + Functional Analysts), Portfolio Management.

Candidate features are identified in your own project or in another team's project. A candidate can either be not yet built (planned in a project's backlog) or already implemented in a project/template and considered generic enough to be adapted and migrated into Common.

To propose a feature:

1. Navigate to the [GitHub repository](https://github.com/criticalmanufacturing/mes-common-library) and select **Issues**.
2. Create a new Issue tagged `feature-request`, with an explicit and complete explanation of the feature to be implemented or incorporated into the repo.
3. Assign a **feature owner** (assignee) — the person initially responsible for implementation and review. This can be changed later.

Once created, the Issue is ready to move into Approval (which can be done in parallel with further internal refinement).

### 2. Approval

**Who:** A selected group of maintainers/advocates (Engineering Leads and Architecture & Advocacy).

Maintainers log into the repository and review proposed feature requests (via a dedicated view listing them), weighing scope and feasibility. As a result, feature requests are either approved or rejected.

### 3. Implementation / Migration

**Who:** The feature-assigned team (from the Issue).

Implementation includes unit testing and documentation, and is approved through **Pull Requests**. This applies both to features implemented from scratch and to features migrated from another project — if a migrated feature did not already have unit tests, they must be developed. Bugs, change requests and version updates follow the same validation process (see [Reporting Bugs](#reporting-bugs)).

Automatic PR and CI pipelines build, pack and test only the changed/target features (not every feature in the repo); once CI-tested, candidate packages are ready to be installed and demonstrated.

#### Ground rules

- **Commit messages:** use conventional prefixes on every commit (`feat:`, `fix:`, `chore:`, ...) — see [Commit Message & Hook Enforcement](#commit-message--hook-enforcement).
- **Pull Request approval:** implementation, documentation and unit tests are mandatory. Every PR must be approved by the feature owner (or another designated reviewer) and linked to an Issue for full traceability.
- **Integration tests:** test the feature integrated into your own project before approving the implementation.
- **One process for all changes:** bugs, change requests and version updates follow the same validation process as new features.

### 4. Internal Demo

*Optional phase, decided by the Feature Owner.*

**Who:** The feature-assigned team.

The implemented functionality is demonstrated to interested parties as a first validation. After internal approval, the feature is ready to be officially released.

### 5. Release and Publish

**Who:** The feature-assigned engineering team.

After official approval, the feature is published and ready to be used in implementation projects. The feature is added to the index in the root [README.md](README.md#features-index) (which works as a feature catalog) on the `main` branch, pointing to the feature's own README for usage, versioning and compatibility details.

### 6. Global Demo

*Optional phase, decided by the Feature Owner.*

**Who:** The feature-assigned functional team (e.g., Functional Analyst(s) or Product Managers).

The new feature (or enhancements to an existing one) is presented to the global Solution Delivery forum (e.g., SD Talks) as a knowledge transfer to the rest of the implementation teams.

## I Want To Contribute

### Legal Notice

When contributing to this project, you must agree that you have authored 100% of the content, that you have the necessary rights to the content and that the content you contribute may be provided under the project licence.

### Reporting Bugs

#### Before Submitting a Bug Report

A good bug report shouldn't leave others needing to chase you up for more information. Therefore, we ask you to investigate carefully, collect information and describe the issue in detail in your report. Please complete the following steps in advance to help us fix any potential bug as fast as possible.

- Make sure that you are using the latest version.
- Determine if your bug is really a bug and not an error on your side e.g. using incompatible environment components/versions (make sure that you have read the feature's documentation. If you are looking for support, you might want to check [this section](#i-have-a-question)).
- To see if other users have experienced (and potentially already solved) the same issue you are having, check if there is not already a bug report existing for your bug or error in the [bug tracker](https://github.com/criticalmanufacturing/mes-common-library/issues?q=label%3Abug).
- Collect information about the bug:
  - Stack trace (Traceback)
  - OS, Platform and Version (Windows, Linux, macOS, x86, ARM)
  - Version of the interpreter, compiler, SDK, runtime environment, package manager, depending on what seems relevant.
  - Possibly your input and the output
  - Can you reliably reproduce the issue? And can you also reproduce it with older versions?
  
#### How Do I Submit a Good Bug Report?

You must never report security related issues, vulnerabilities or bugs including sensitive information to the issue tracker, or elsewhere in public.

We use GitHub issues to track bugs and errors. If you run into an issue with the project:

- Open an [Issue](https://github.com/criticalmanufacturing/mes-common-library/issues/new).
- Explain the behavior you would expect and the actual behavior.
- Please provide as much context as possible and describe the *reproduction steps* that someone else can follow to recreate the issue on their own. This usually includes your code. For good bug reports you should isolate the problem and create a reduced test case.
- Provide the information you collected in the previous section.

Please note that there isn't always a dedicated team assigned to a given feature, so if you're able to, you're encouraged to [implement the fix yourself](#your-first-code-contribution). __Before opening a Pull Request, confirm with the feature owner (or, if there isn't one assigned, any maintainer)__ whether the fix is actually appropriate and should be approved.

Bug fixes, change requests and version updates don't go through the [Identify](#1-identify)/[Approval](#2-approval) phases of the [Feature Lifecycle](#feature-lifecycle) — they go straight through the same PR-based validation process described in [Implementation / Migration](#3-implementation--migration).

> **Suggesting a new feature?** Instead of a generic enhancement suggestion, new feature ideas for this repository follow the dedicated [Feature Lifecycle](#feature-lifecycle) process below, starting at [Identify](#1-identify).

### Your First Code Contribution

To setup your environment use the Getting Started Guide on Critical Manufacturing [Developer Portal](https://developer.criticalmanufacturing.com).

### Commit Message & Hook Enforcement

This repository uses [Husky](https://typicode.github.io/husky/) to run git hooks and [commitlint](https://commitlint.js.org/) to check commit messages against the [Conventional Commits](https://www.conventionalcommits.org/) format (configured in `.commitlintrc.json`), e.g.:

```
fix: correct offset calculation in the IoT persistency task
feat(IoTMTConnect): add support for MTConnect adapters over TLS
```

Two hooks are installed automatically the first time you run `npm install` at the repository root (this also happens automatically when the devcontainer is created, via `postCreateCommand`):

- **`commit-msg`** — rejects commit messages that don't follow Conventional Commits.
- **`pre-commit`** — runs `.github/scripts/validate-package-ids-versions.sh`, rejecting the commit if any `cmfpackage.json` version doesn't follow the [`{MES_MAJOR}{MES_MINOR}{MES_PATCH}{MAJOR}.{MINOR}.{PATCH}` rule](README.md#features-versioning).

Both checks are also enforced in CI as a backstop, but the hooks give you the same feedback locally before you push.
