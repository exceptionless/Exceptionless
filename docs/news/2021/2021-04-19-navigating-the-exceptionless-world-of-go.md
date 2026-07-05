---
title: "Navigating The Exceptionless World of Go"
date: 2021-04-19
---

# Navigating The Exceptionless World of Go

Go does not have the concept of exceptions. Welp, I guess [Exceptionless](https://exceptionless.com) doesn't apply. Let's pack it up and head home, everyone. 

Kidding, of course. While Go really doesn't have the concept of exceptions, errors still happen in Go codebases, and those errors need to be handled. Having recently built an Exceptionless client in Go, we had the opportunity to take a deep dive into the Go programming language, and we'd like to share some of what we discovered along the way. 

### No Scrubs, No Exceptions

![Gif of No Scrubs music video](https://media.giphy.com/media/ty48ztZKklU6k/giphy.gif) 

Let's take a quick look at why the Go programming language doesn't have exceptions, and then we can dive into how we work around this. From the Go FAQ: 

> We believe that coupling exceptions to a control structure, as in the try-catch-finally idiom, results in convoluted code. It also tends to encourage programmers to label too many ordinary errors, such as failing to open a file, as exceptional. 

Essentially, Go doesn't want any scrubs (try/catch paradigm) hanging out the passenger side of their best friend's (that's you) ride trying to call things exceptions. Not everything is fatal. Not everything is an exception. Errors can happen without being so extraordinary to be considered an exception. As such, Go made the opinionated decision to simply not acknowledge exceptions as a construct. 

For those instances where an error is truly catastrophic, Go has some build in mechanism to handle and recover. Those are [documented here](https://blog.golang.org/defer-panic-and-recover). 

So with no exceptions, how should errors be handled when using Exceptionless?

### No Exceptions != No Errors

Just because there are no exceptions doesn't mean there are no errors. Fortunately, Go has a well-documented paradigm for handling errors without the need for something like an exception. 

Every function you write in Go can have multiple return values. So, a well-written function will return both an error and the actual function's response. Let's take a look at a very basic example of this. 

```go
package main 

import ( 
	"github.com/go-errors/errors"
)

func CombineStrings(string1 string, string2 string) (string, error) {
	var errToReturn error
	
  if string1 == "" || string2 == "" {
    errToReturn = errors.New(fmt.Sprintf("Strings must have at least one character"))
  }

  returnValue := string1 + string2
	return returnValue, errToReturn
}
```

In this example, we are using the `go-errors` module to build an error of the type `error`. We do a simple check to see if both of the string arguments to our function are not empty. If either is empty, we assign an error to the variable `errToReturn`. Then, in our return statement, we return both the expected return value of the function and the error. 

When calling this function, the developer needs to first check for an error before moving on. That might look like this: 

```go
combinedString, err := CombineString("hello, ", "world")

if err != nil {
  //Handle Error
}

fmt.Print(combinedString)
```

If an error is so bad that your code cannot execute any further, you may want to call Go's built in handler, `panic`, like this: 

```go
if err != nil {
  panic(err)
}
```

### Ok, So How Do We Use Exceptionless in an Exceptionless Environment?

Exceptionless (that's us) is a way of thinking about your code. It does not actually rely on the concepts of exceptions. In fact, Exceptionless is so much more than error/exception handling. 

So, with that framing, it becomes a lot more clear that Exceptionless can very easily be used within Go code to handle errors. In fact, the structure for using Exceptionless in Go is not all that different from other programming languages. 

We have a (currently as of the writing of this post) beta version of a [Go Exceptionless client](https://github.com/exceptionless/Exceptionless.Go). You can find that here. I'll show you how to use the client, but do note that the client API could change until it's out of beta. The goal here is less about showing off the Go client and more about showing you how to handle errors with an event monitoring service within Go. 

The first thing you'll need to do is sign up for Exceptionless and get an API Key. [You can sign up here](https://exceptionless.com). Once you've done that, you'll need to install the Exceptionless Go client in your project. To do so, simply run the following: 

```
go get https://github.com/exceptionless/Exceptionless.Go
```

Once that's installed, import it into your project like this: 

```go
import (
  "github.com/exceptionless/Exceptionless.Go"
)
```

Now, let's take a look at how we would use the client. On app startup (probably in your `main` function), you can initialize and configure the client like this: 

```go
func main() {
  config := exceptionless.Exceptionless{
    ApiKey: "YOUR API KEY"
  }
  exceptionless.Configure(config)
}
```

With the Exceptionless client configured, you can now use it to handle all sorts of events in your app. We're going to focus in on exception—or, not exceptions as the case might be with go 😉.

Let's use our example function from earlier as the example. 

```go
package main 

import ( 
	"github.com/go-errors/errors"
  "github.com/exceptionless/Exceptionless.Go"
)

func SomOtherFunction() {
  combinedString, err := CombineString("", "world")

if err != nil {
  exceptionless.SubmitError(err)
}

fmt.Print(combinedString)
}

func CombineStrings(string1 string, string2 string) (string, error) {
	var errToReturn error
	
  if string1 == "" || string2 == "" {
    errToReturn = errors.New(fmt.Sprintf("Strings must have at least one character"))
  }

  returnValue := string1 + string2
	return returnValue, errToReturn
}
```

If you remember the `CombineStrings` function will return an error if either string is empty. So in my mock function `SomeOtherFunction`, I am calling `CombineStrings` and passing in an empty string. When the error comes back, all we need to do is handle it with Exceptionless using `exceptionless.SubmitError()`. Simple as that. 

### Conclusion

There is a lot more you could do. The Exceptionless Go client allows you to build custom events with more info. You could even bypass the client entirely and make an http request directly to the [Exceptionless API](https://api.exceptionless.com) in your error handler. 

But the point is, despite Go being an exceptionless programming environment, you can catch errors and report them. So that means Exceptionless is actually a match made in heaven for the *exceptionless* environment of Go. 