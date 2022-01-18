name: MajorMUD bug report
description: File a bug report for MajorMUD
title: "[WCCMMUD - MajorMUD]: "
labels: ["bug", "module"]
assignees:
body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to fill out this bug report!
    validations:
      required: false
  - type: dropdown
    id: version
    attributes:
      label: Version
      description: What version of our software are you running?
      options:
        - 1.11p DOS MBBSEmu ready
        - Some other version (we may not support, but please include version number below and we will consider)
    validations:
      required: true
  - type: input
    id: non-standard-version
    attributes:
      label: Non-standard Version
      description: What non-1.11p MBBSEmu-ready version of MajorMUD are you running?
      placeholder: ex. 1.11n
  - type: dropdown
    id: operating-system
    attributes:
      label: What operating system(s) are you seeing the problem on?
      multiple: true
      options:
        - Windows x86
        - Linux x86
        - Linux ARM
        - macOS x86
        - macOS ARM
  - type: dropdown
    id: client
    attributes:
      label: What client(s) are you seeing the problem on?
      multiple: true
      options:
        - MegaMud 1.03u
        - Windows PuTTY
        - Linux or macOS telnet
        - Other (include below)
  - type: textarea
    id: what-happened
    attributes:
      label: What happened?
      description: Also tell us, what did you expect to happen?
      placeholder: Tell us what you see!
      value: "A bug happened!"
    validations:
      required: true
  - type: textarea
    id: logs
    attributes:
      label: Relevant log output
      description: Please copy and paste any relevant log output. This will be automatically formatted into code, so no need for backticks.
      render: shell
  - type: checkboxes
    id: terms
    attributes:
      label: Code of Conduct
      description: By submitting this issue, you agree to follow our [Code of Conduct](https://example.com)
      options:
        - label: I agree to follow this project's Code of Conduct
          required: true
