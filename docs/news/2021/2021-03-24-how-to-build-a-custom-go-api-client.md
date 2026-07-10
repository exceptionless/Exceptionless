---
title: "How to Build a Custom Go Client For a REST API"
---

# How to Build a Custom Go Client For a REST API

![Picture of gophers working](./go_gophers.jpg)
    
[Exceptionless](https://exceptionless.com) is powered by a REST API. When you interact with the dashboard UI, when you use the .NET client, and when you use the JavaScript client, you are interacting with the REST API. It is well-documented, and it can be used without any client libraries. This paradigm makes it simple for developers to create their own wrappers around the API. In fact, we recently started work on building an official Go client for Exceptionless. Along the way, we learned some tips and tricks that may be helpful for others that want to build clients and SDKs in Go that wrap RESTful APIs. 

First, a little about Go. [Go](https://golang.org/) is a statically typed language, built originally by the folks at Google. Go, while close in syntax to many other statically typed languages, differs in that it is no object oriented. Go is also very well suited for [gRPC APIs](https://www.programmableweb.com/news/what-grpc-api-and-how-does-it-work/analysis/2020/10/08), but that does not prevent it from being used with REST APIs, as we'll see here today. 

### Getting Started

In order to build our Go client, we will need to have Go installed. Honestly, this can be the hardest step as it involves setting environment variables and updating your profile source PATH. So rather than risk confusing you with the steps to install Go and get started, I'm going to simply point you to Go's official install instructions. 

[You can find those instructions here](https://golang.org/doc/install).

Once you've installed Go, you will need to have a text editor handy so that we can write our new Go code. From the command line, create a new folder and call it "go-rest". Change into that directory, and let's start writing some code. 

### The Main File

In Go, you will always have a `main.go` file which acts as the entry point for your source code. We need to set that up first, so let's do that now. In the root of your project folder, create your `main.go` file. Inside that file, let's start by declaring our package and importing a module. Add the following: 

```go
package main

import (
	"fmt"
)
```

Your file won't do anything yet, but we're laying the groundwork. We have declared our package as `main`, and we have imported the built-in `fmt` library from Go for formatting. 

Next, we need a `main` function, so let's create that. Add the following below your import statement: 

```go
func main() {
  fmt.Println("Hello, world")
}
```

This is the example program Go's example docs show, so we might as well run it. From your command line, inside your project directory, run this command: 

```
go run .
```

You should see `Hello, world` printed in the command line terminal window. 

Now that we have the fundamentals down, let's talk about how Go works so that we can build our REST API client. You can include as many functions in your `main.go` file as you'd like and you can call those function from within other functions. But, like any other programming language, it's probably smart to separate code to make it easier to work with. 

### Creating an API Helper

The nice thing about Go is that when you create a new file, that file is automatically available from any of your other files as long as they share the same main package. 

Since we are building a REST client, it probably makes sense to create a file that would handle all our API routing request. So, create a file in the root of your project called `api.go`. 

Inside that file, make sure to reference the main package at the top like this: 

`pacakage main`

We are also going to import a couple packages here as well, so your file should look like this: 

```go
package main

import (
	"bytes"
	"log"
	"net/http"
)
```

These packages are all built into Go itself. You can install external packages as well, and we'll explore that soon. 

Now that we have the start of our API file, it's good to think about what our client needs to do. With a REST API, you may have the following request methods: 

* GET
* POST
* PUT
* DELETE
* PATCH

You may not need all of these for your client, but it's good to know that they exist. In our case, we are going to implement the GET and POST methods and with those as a template, you should be able to extend your code to implement PUT, PATCH, and DELETE. 

Let's start by building the POST method since its the backbone of our client. In your `api.go` file, below the import statement, add the following: 

```go
//Post posts to the Exceptionless Server
func Post(endpoint string, postBody string, authorization string) string {
	baseURL := "YOUR API URL/"
	url := baseURL + endpoint
	var jsonStr = []byte(postBody)
	req, err := http.NewRequest("POST", url, bytes.NewBuffer(jsonStr))
	req.Header.Set("Authorization", "Bearer "+authorization)
	req.Header.Set("Content-Type", "application/json")
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		panic(err)
	}
	defer resp.Body.Close()
	return string(resp.Status)
}
```

In our real-world use case, we are making requests to the Exceptionless API, so we know the post body needs to be a JSON string. This is why the `postBody` is of type `string`. If your API is expecting a different format, make sure you type your variable properly here. The other two arguments in our `Post` function are pretty self explanatory. The `endpoint` string is the endpoint on your API you want to call. The `authorization` string is the token/API key needed to authenticate into the API. You could choose to handle the authorization differently, if you wanted. For example, if your API expected basic authentication, your authorization variable might be a string mapping of username and password. 

One of the tricks here is if you are sending JSON to your REST API, you will need to convert the body into a format the http client library within Go can handle. We're doing that with the `bytes.NewBuffer(jsonStr)` call. 

Now, let's put together our `GET` function: 

```go
//GET makes api GET requests
func Get(endpoint string, authorization string) map[string]interface{} {
	baseURL := "YOUR API URL/"

	url := baseURL + endpoint

	httpClient := &http.Client{}
	req, err := http.NewRequest("GET", url, nil)

	if err != nil {
		fmt.Println(err)
	}

	req.Header.Add("accept", "application/json")
	req.Header.Add("Authorization", "Bearer "+authorization)

	res, err := httpClient.Do(req)
	if err != nil {
		fmt.Println(err)
	}
	defer res.Body.Close()

	body, err := ioutil.ReadAll(res.Body)
	if err != nil {
		fmt.Println(err)
	}

	var result map[string]interface{}
	json.Unmarshal([]byte(body), &result)
	return result
}
```

Much like out `POST` request, our `GET` request takes in arguments. We only need the `endpoint` and the `authorization` arguments for this function. This function is pretty straight forward. However, if you want to read the response as JSON, you need to take an extra step as I've shown above. 

You will want to a string mapping by unmarshaling the JSON returned by the API. Of course, your API may not return JSON, so use this accordingly. If you do need to unmarshal the JSON, you simply need to pass the response body into the `json.Unmarshal()` as shown above. 

These two functions should help you build your other REST-related functions. Now, let's take a look at helper functions that will make your client easy to use while sending the correct data to your API. 

## Convenience Functions

A good SDK or client API wrapper will include helper functions so the developer using it doesn't have to still manually build requests to your API. The best way to build helper functions is to start with your data model. Let's say, for example, your API expects a JSON payload like this: 

```json
{
	"BookTitle": "The Great Gatsby", 
	"Author": "F. Scott Fitzgerald", 
	"Rating": 7
}
```

In this case, we'd probably want to create a struct type variable that we can use to build our payload for the reqest. That might look like this: 

```go
type BookRating struct {
	BookTitle       string
	Author          string
	Rating 					uint
}
```

A quick note on Go variables and functions. If the variable or the function name is capitalized, it is exported and available throughout your program. 

Now that we have a struct we can use, we can start to build a helper function that would build a payload for our API. In keeping with the example in the JSON and the struct above, let's pretend our API take a `POST` request to rate a specific book. For some reason, our API needs the string title and string author of the book, and it needs an interger for the rating. You might create a helper function like this: 

```go
func RateBook(title string, author string, rating uint): bool {
	newRating := BookRating{
		BookTitle: title, 
		Author: author, 
		Rating: rating
	}

	json, err := json.Marshal(newRating)
	if err != nil {
		fmt.Println(err)
		return false
	}

	resp, err := Post("rateBook", string(json), "API KEY")
	if err != nil {
		fmt.Println(err)
		return false
	}	

	return true
}
```

In the `RateBook` function, we are allowing the developer to simply pass in the title, author, and the rating. We then build the JSON payload for the developer and send it to the `Post` function we created earlier. When we are building the JSON payload, we must use `json.Marshal` to convert our struct to a type that can be used with our REST API. 

You'll note, the authorization argument in the above example is "API KEY", but a good SDK will have stored that API Key when the client was initialized. I'll leave it up to you on how you'd like to handle this, but it could be as simple as calling `Configure` function with the developer's API Key and storing the key in memory. 

## Wrapping Up

This is a simple example of how you might build a Go client for a REST API. The concepts are general, but they hopefully help you if you find yourself needing to build your own client. Exceptionless will be launching its own Go client soon. If you haven't tried Exceptionless for your application's event monitoring, [give it a shot now](https://exceptionless.com).


