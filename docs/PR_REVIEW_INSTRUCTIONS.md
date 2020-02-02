# Reviewing a Pull Request (PR)

In addition to code review (via online diffs), you'll also need to build the PR locally to test it in-game.

## Before you start

If you haven't already done so, work through the [BUILDING_INSTRUCTIONS](./BUILDING_INSTRUCTIONS.md) guide to ensure your environment is set up, configured and building properly.

## Cloning the PR

#### GitHub Desktop:

* Choose the PR from the **Current Branch** drop-down
* If prompted, choose **Switch Branch** (don't change anything on the dialog)
* If the panel at the top shows an option to `Pull`, click it to pull any recent commits

#### Git for windows:

* You'll need the PR number (shown at end of URL in the PR online)
* Open the inbuilt console
* Inside project folder `\<Folder_of_cloned_repository>\TLM\`:
    * `git fetch origin pull/<pr number>/head:<branch name>`
    * For example: `git fetch origin pull/123/head:PR_123` for PR #123
* Switch to new branch:
    * `git checkout <branch name>`
    * For example: `git checkout PR_123`

#### In the IDE:

* You can also clone a PR directly within the IDE
* Use branch switch menu - it's usually in the bottom-right corner
* If you don't see the PR branch, refresh branches:
    * Visual Studio:
        * Open **Team Explorer** (tab on the bottom of Solution Explorer panel in the sidebar)
        * Click the blue **Refresh** icon (next to the green plug icon under the Team Explorer panel title bar)
    * JetBrains Rider:
        * Choose **VCS** -> **Git** -> **Fetch**

## Building the PR

See the **Build** section in [BUILDING_INSTRUCTIONS](./BUILDING_INSTRUCTIONS.md) guide