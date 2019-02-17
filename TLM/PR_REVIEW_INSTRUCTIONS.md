
## Pull Request review


#### Clone PR as new branch inside repository folder:

* get __PR__ index - this number with __#__ after __PR__ name
* using preferred __git console__ inside project folder ``` \<Folder_of_cloned_repository>\TLM\ ```
  * type ```git fetch origin pull/<pr_number_skip_#>/head:<your_new_branch_name>``` _(skip <>)_ e.g. ```git fetch origin pull/123/head:PR_123```
* to switch to newly created branch:
  * type ```git checkout <your_new_branch_name>``` _(skip <>)_ e.g. ```git checkout PR_123```
  * or use branch switch menu _(usually bottom right corner of preferred __IDE__)_

##### If you don't see new branch to select try to refresh available branches:
 * __VS 2017:__ _TeamExplorer -> Refresh_
 * __JB Raider:__ _VCS -> Git -> Fetch_

#### Now newly created branch should be accessible from branch switch menu
