Imports System.IO
Imports System.Data.SQLite
Imports Microsoft.Web.WebView2.Core
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.Runtime.InteropServices

Public Class Form1

    <DllImport("credui.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CredUIPromptForWindowsCredentials(
        ByRef pUiInfo As CREDUI_INFO,
        authError As Integer,
        ByRef authPackage As UInteger,
        InAuthBuffer As IntPtr,
        InAuthBufferSize As UInteger,
        ByRef refOutAuthBuffer As IntPtr,
        ByRef refOutAuthBufferSize As UInteger,
        ByRef fSave As Boolean,
        flags As Integer) As Integer
    End Function

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure CREDUI_INFO
        Public cbSize As Integer
        Public hwndParent As IntPtr
        Public pszMessageText As String
        Public pszCaptionText As String
        Public hbmBanner As IntPtr
    End Structure

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        InitializeDatabase()

        Await WebView21.EnsureCoreWebView2Async()

        Dim htmlPath As String =
            Path.Combine(Application.StartupPath, "html\index.html")

        WebView21.Source = New Uri(htmlPath)

        AddHandler WebView21.CoreWebView2.WebMessageReceived,
            AddressOf WebMessageReceived

    End Sub


    Private Async Sub WebMessageReceived(sender As Object,
                                   e As CoreWebView2WebMessageReceivedEventArgs)

        Dim json As String = e.WebMessageAsJson
        Dim request As JObject = JObject.Parse(json)

        Dim action As String = request("action").ToString()

        Select Case action
            Case "getProfileDetails"

                Dim uid = Convert.ToInt32(request("userId"))

                Dim skills As New List(Of Object)

                Using conn As New SQLiteConnection(connectionString)

                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                    "SELECT Title, Category, CreditsRequired
         FROM Skills WHERE UserID=@u", conn)

                    cmd.Parameters.AddWithValue("@u", uid)

                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            skills.Add(New With {
                                .Title = reader("Title"),
                                .Category = reader("Category"),
                                .Credits = reader("CreditsRequired")
                            })
                        End While
                    End Using

                End Using

                Dim response = JsonConvert.SerializeObject(New With {
                    .type = "profileDetails",
                    .skills = skills
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(response)

            Case "getProfiles"

                Dim users As New List(Of Object)

                Using conn As New SQLiteConnection(connectionString)

                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                    "SELECT UserID, Name, Area, Credits, Rating FROM Users", conn)

                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            users.Add(New With {
                                .UserID = reader("UserID"),
                                .Name = reader("Name"),
                                .Area = reader("Area"),
                                .Credits = reader("Credits"),
                                .Rating = reader("Rating")
                            })
                        End While
                    End Using

                End Using

                Dim response = JsonConvert.SerializeObject(New With {
                    .type = "profiles",
                    .users = users
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(response)

            ' ================= REGISTER =================
            Case "registerUser"
                Try
                    Dim userId As Integer = Convert.ToInt32(request("userId"))
                    ' Prevent negative or zero UserID
                    If userId <= 0 Then
                        Dim errResp = JsonConvert.SerializeObject(New With {
                            .type = "error",
                            .message = "User ID must be a positive number."
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                        Exit Sub
                    End If
                    Dim name As String = request("name").ToString().Trim()
                    Dim area As String = request("area").ToString().Trim()

                    If String.IsNullOrEmpty(name) Then
                        Dim errResp = JsonConvert.SerializeObject(New With {
                            .type = "error",
                            .message = "Name cannot be empty."
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                        Exit Sub
                    End If

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    ' Check if ID already exists
                    Dim checkCmd As New SQLiteCommand(
                        "SELECT COUNT(*) FROM Users WHERE UserID=@id", conn)
                    checkCmd.Parameters.AddWithValue("@id", userId)

                    Dim exists As Integer = Convert.ToInt32(checkCmd.ExecuteScalar())

                    If exists > 0 Then
                        Dim errResp = JsonConvert.SerializeObject(New With {
                            .type = "error",
                            .message = "User ID already exists. Choose another."
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                        Exit Sub
                    End If

                    Dim cmd As New SQLiteCommand(
                        "INSERT INTO Users (UserID, Name, Area, Credits, Rating)
             VALUES (@id,@name,@area,50,0)", conn) ' Starting with 50 credits for easier testing

                    cmd.Parameters.AddWithValue("@id", userId)
                    cmd.Parameters.AddWithValue("@name", name)
                    cmd.Parameters.AddWithValue("@area", area)

                    cmd.ExecuteNonQuery()

                    ' Automatically log them in by fetching their data
                    Dim loginCmd As New SQLiteCommand("SELECT UserID, Name, Area, Credits, Rating FROM Users WHERE UserID=@id", conn)
                    loginCmd.Parameters.AddWithValue("@id", userId)
                    Using reader = loginCmd.ExecuteReader()
                        If reader.Read() Then
                            Dim response = JsonConvert.SerializeObject(New With {
                                .type = "loginSuccess",
                                .user = New With {
                                    .UserID = reader("UserID"),
                                    .Name = reader("Name"),
                                    .Area = reader("Area"),
                                    .Credits = reader("Credits"),
                                    .Rating = reader("Rating")
                                },
                                .message = "User registered successfully!"
                            })
                            WebView21.CoreWebView2.PostWebMessageAsJson(response)
                        End If
                    End Using

                End Using
            Catch ex As Exception
                Dim errResp = JsonConvert.SerializeObject(New With {
                    .type = "error",
                    .message = "Registration failed: " & ex.Message
                })
                WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
            End Try

            ' ================= LOGIN =================
            Case "loginUser"
                Try
                    ' Check if this is an explicit login rather than a background refresh
                    Dim isBackgroundRefresh = False
                    If request("refresh") IsNot Nothing Then
                        isBackgroundRefresh = request("refresh").ToObject(Of Boolean)()
                    End If

                    If Not isBackgroundRefresh Then
                        Dim credUiInfo As New CREDUI_INFO()
                        credUiInfo.cbSize = Marshal.SizeOf(credUiInfo)
                        credUiInfo.pszCaptionText = "Authentication Required"
                        credUiInfo.pszMessageText = "Please authenticate using Windows Hello or your Windows PIN/Password."
                        credUiInfo.hwndParent = Me.Handle ' Set parent window to block UI
                        
                        Dim authPackage As UInteger = 0
                        Dim outAuthBuffer As IntPtr = IntPtr.Zero
                        Dim outAuthBufferSize As UInteger = 0
                        Dim save As Boolean = False
                        
                        Dim result As Integer = CredUIPromptForWindowsCredentials(
                            credUiInfo, 0, authPackage, IntPtr.Zero, 0, 
                            outAuthBuffer, outAuthBufferSize, save, 1) ' 1 = CREDUIWIN_GENERIC
                            
                        If outAuthBuffer <> IntPtr.Zero Then
                            Marshal.FreeCoTaskMem(outAuthBuffer)
                        End If
                        
                        If result <> 0 Then
                            Dim errResp = JsonConvert.SerializeObject(New With {
                                .type = "error",
                                .message = "Authentication failed or was cancelled."
                            })
                            WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                            Exit Sub
                        End If
                    End If

                    Dim userId As Integer = Convert.ToInt32(request("userId"))
                    Using conn As New SQLiteConnection(connectionString)
                        conn.Open()
                        Dim cmd As New SQLiteCommand("SELECT UserID, Name, Area, Credits, Rating FROM Users WHERE UserID=@id", conn)
                        cmd.Parameters.AddWithValue("@id", userId)
                        Using reader = cmd.ExecuteReader()
                            If reader.Read() Then
                                Dim response = JsonConvert.SerializeObject(New With {
                                    .type = "loginSuccess",
                                    .user = New With {
                                        .UserID = reader("UserID"),
                                        .Name = reader("Name"),
                                        .Area = reader("Area"),
                                        .Credits = reader("Credits"),
                                        .Rating = reader("Rating")
                                    },
                                    .message = "Login successful!"
                                })
                                WebView21.CoreWebView2.PostWebMessageAsJson(response)
                            Else
                                Dim errResp = JsonConvert.SerializeObject(New With {
                                    .type = "error",
                                    .message = "User not found."
                                })
                                WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                            End If
                        End Using
                    End Using
                Catch ex As Exception
                    Dim errResp = JsonConvert.SerializeObject(New With {
                        .type = "error",
                        .message = "Login failed: " & ex.Message
                    })
                    WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                End Try




            ' ================= ADD SKILL =================
            Case "addSkill"
                Try
                    Using conn As New SQLiteConnection(connectionString)
                        conn.Open()

                        Dim cmd As New SQLiteCommand(
                            "INSERT INTO Skills 
                            (UserID, Title, Category, Description, CreditsRequired)
                            VALUES (@uid,@title,@cat,@desc,@cred)", conn)

                        cmd.Parameters.AddWithValue("@uid", Convert.ToInt32(request("userId")))
                        cmd.Parameters.AddWithValue("@title", request("title").ToString())
                        cmd.Parameters.AddWithValue("@cat", request("category").ToString())
                        cmd.Parameters.AddWithValue("@desc", request("description").ToString()) ' Need description param from frontend
                        cmd.Parameters.AddWithValue("@cred", Convert.ToInt32(request("credits")))

                        cmd.ExecuteNonQuery()
                        
                        Dim response = JsonConvert.SerializeObject(New With {
                            .type = "success",
                            .message = "Skill added successfully!"
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(response)
                    End Using
                Catch ex As Exception
                    Dim errResp = JsonConvert.SerializeObject(New With {
                        .type = "error",
                        .message = "Failed to add skill: " & ex.Message
                    })
                    WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                End Try


            ' ================= POST GIG =================
            Case "postGig"
                Try
                    Dim credits As Integer = Convert.ToInt32(request("credits"))
                    If credits <= 0 Then
                        Dim errResp = JsonConvert.SerializeObject(New With {
                            .type = "error",
                            .message = "Credits offered must be greater than zero."
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                        Exit Sub
                    End If

                    Using conn As New SQLiteConnection(connectionString)
                        conn.Open()

                        Dim cmd As New SQLiteCommand(
                            "INSERT INTO Gigs
                            (PostedBy, Category, Description, CreditsOffered, Status)
                            VALUES (@uid,@cat,@desc,@cred,'Open')", conn)

                        cmd.Parameters.AddWithValue("@uid", Convert.ToInt32(request("userId")))
                        cmd.Parameters.AddWithValue("@cat", request("category").ToString())
                        cmd.Parameters.AddWithValue("@desc", request("description").ToString())
                        cmd.Parameters.AddWithValue("@cred", credits)

                        cmd.ExecuteNonQuery()
                        
                        Dim response = JsonConvert.SerializeObject(New With {
                            .type = "success",
                            .message = "Gig posted successfully!"
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(response)
                    End Using
                Catch ex As Exception
                    Dim errResp = JsonConvert.SerializeObject(New With {
                        .type = "error",
                        .message = "Failed to post gig: " & ex.Message
                    })
                    WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                End Try


            ' ================= GET USERS =================
            Case "getUsers"

                Dim users As New List(Of Object)

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "SELECT UserID, Name, Credits FROM Users", conn)

                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            users.Add(New With {
                                .UserID = reader("UserID"),
                                .Name = reader("Name"),
                                .Credits = reader("Credits")
                            })
                        End While
                    End Using
                End Using

                Dim responseUsers = JsonConvert.SerializeObject(New With {
                    .type = "users",
                    .users = users
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(responseUsers)


            ' ================= GET GIGS =================
            Case "getGigs"

                Dim gigs As New List(Of Object)

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "SELECT GigID, PostedBy, Description, CreditsOffered 
                         FROM Gigs WHERE Status='Open'", conn)

                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            gigs.Add(New With {
                                .GigID = reader("GigID"),
                                .PostedBy = reader("PostedBy"),
                                .Description = reader("Description"),
                                .CreditsOffered = reader("CreditsOffered")
                            })
                        End While
                    End Using
                End Using

                Dim responseGigs = JsonConvert.SerializeObject(New With {
                    .type = "gigs",
                    .gigs = gigs
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(responseGigs)


            Case "completeGig"

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()
                    Dim transaction = conn.BeginTransaction()

                    Try
                        Dim gigId = Convert.ToInt32(request("gigId"))
                        Dim postedBy = Convert.ToInt32(request("postedBy"))
                        Dim worker = Convert.ToInt32(request("worker"))
                        Dim credits = Convert.ToInt32(request("credits"))

                        ' ================= VALIDATIONS =================

                        ' Prevent self-accept
                        If postedBy = worker Then
                            transaction.Rollback()
                            Exit Sub
                        End If

                        ' Check if gig is still open
                        Dim checkGig As New SQLiteCommand(
                            "SELECT Status FROM Gigs WHERE GigID=@g", conn)
                        checkGig.Parameters.AddWithValue("@g", gigId)
                        Dim status = checkGig.ExecuteScalar()

                        If status Is Nothing OrElse status.ToString() <> "Open" Then
                            transaction.Rollback()
                            Exit Sub
                        End If

                        ' Check if poster has enough credits
                        Dim checkCredits As New SQLiteCommand(
                            "SELECT Credits FROM Users WHERE UserID=@u", conn)
                        checkCredits.Parameters.AddWithValue("@u", postedBy)
                        Dim currentCredits = Convert.ToInt32(checkCredits.ExecuteScalar())

                        If currentCredits < credits OrElse credits <= 0 Then
                            transaction.Rollback()
                            Exit Sub
                        End If

                        ' ================= EXECUTION =================

                        ' Deduct credits
                        Dim cmd1 As New SQLiteCommand(
                            "UPDATE Users SET Credits = Credits - @c WHERE UserID=@u", conn)
                        cmd1.Parameters.AddWithValue("@c", credits)
                        cmd1.Parameters.AddWithValue("@u", postedBy)
                        cmd1.ExecuteNonQuery()

                        ' Add credits
                        Dim cmd2 As New SQLiteCommand(
                            "UPDATE Users SET Credits = Credits + @c WHERE UserID=@u", conn)
                        cmd2.Parameters.AddWithValue("@c", credits)
                        cmd2.Parameters.AddWithValue("@u", worker)
                        cmd2.ExecuteNonQuery()

                        ' Mark gig completed
                        Dim cmd3 As New SQLiteCommand(
                            "UPDATE Gigs SET Status='Completed' WHERE GigID=@g", conn)
                        cmd3.Parameters.AddWithValue("@g", gigId)
                        cmd3.ExecuteNonQuery()

                        ' Insert transaction record
                        Dim cmd4 As New SQLiteCommand(
                            "INSERT INTO Transactions
                 (FromUser, ToUser, Credits, Date)
                 VALUES (@f,@t,@c,@d)", conn)
                        cmd4.Parameters.AddWithValue("@f", postedBy)
                        cmd4.Parameters.AddWithValue("@t", worker)
                        cmd4.Parameters.AddWithValue("@c", credits)
                        cmd4.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                        cmd4.ExecuteNonQuery()

                        transaction.Commit()
                        
                        Dim response = JsonConvert.SerializeObject(New With {
                            .type = "success",
                            .message = "Gig completed successfully!"
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(response)
                        
                    Catch ex As Exception
                        transaction.Rollback()
                        Dim errResp = JsonConvert.SerializeObject(New With {
                            .type = "error",
                            .message = "Transaction failed: " & ex.Message
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                    End Try
                End Using
                
            ' ================= HIRE SKILL =================
            Case "hireSkill"
                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()
                    Dim transaction = conn.BeginTransaction()

                    Try
                        Dim buyerId = Convert.ToInt32(request("buyerId"))
                        Dim sellerId = Convert.ToInt32(request("sellerId"))
                        Dim credits = Convert.ToInt32(request("credits"))
                        Dim skillTitle = request("skillTitle").ToString()

                        If buyerId = sellerId Then
                            transaction.Rollback()
                            Dim errResp = JsonConvert.SerializeObject(New With {
                                .type = "error",
                                .message = "You cannot hire yourself."
                            })
                            WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                            Exit Sub
                        End If

                        ' Check if buyer has enough credits
                        Dim checkHireCredits As New SQLiteCommand("SELECT Credits FROM Users WHERE UserID=@u", conn)
                        checkHireCredits.Parameters.AddWithValue("@u", buyerId)
                        Dim currentHireCredits = Convert.ToInt32(checkHireCredits.ExecuteScalar())

                        If currentHireCredits < credits OrElse credits <= 0 Then
                            transaction.Rollback()
                            Dim errResp = JsonConvert.SerializeObject(New With {
                                .type = "error",
                                .message = "Insufficient credits to hire this skill."
                            })
                            WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                            Exit Sub
                        End If

                        ' Deduct credits
                        Dim cmd1 As New SQLiteCommand("UPDATE Users SET Credits = Credits - @c WHERE UserID=@u", conn)
                        cmd1.Parameters.AddWithValue("@c", credits)
                        cmd1.Parameters.AddWithValue("@u", buyerId)
                        cmd1.ExecuteNonQuery()

                        ' Add credits
                        Dim cmd2 As New SQLiteCommand("UPDATE Users SET Credits = Credits + @c WHERE UserID=@u", conn)
                        cmd2.Parameters.AddWithValue("@c", credits)
                        cmd2.Parameters.AddWithValue("@u", sellerId)
                        cmd2.ExecuteNonQuery()

                        ' Insert transaction record
                        Dim cmd4 As New SQLiteCommand(
                            "INSERT INTO Transactions (FromUser, ToUser, Credits, Date) VALUES (@f,@t,@c,@d)", conn)
                        cmd4.Parameters.AddWithValue("@f", buyerId)
                        cmd4.Parameters.AddWithValue("@t", sellerId)
                        cmd4.Parameters.AddWithValue("@c", credits)
                        cmd4.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                        cmd4.ExecuteNonQuery()

                        transaction.Commit()

                        Dim response = JsonConvert.SerializeObject(New With {
                            .type = "hireSuccess",
                            .message = $"Successfully hired {sellerId} for {skillTitle}!"
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(response)

                    Catch ex As Exception
                        transaction.Rollback()
                        Dim errResp = JsonConvert.SerializeObject(New With {
                            .type = "error",
                            .message = "Hire transaction failed: " & ex.Message
                        })
                        WebView21.CoreWebView2.PostWebMessageAsJson(errResp)
                    End Try
                End Using

            ' ================= CHART DATA =================
            Case "getChart"

                Dim labels As New List(Of String)
                Dim values As New List(Of Integer)

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "SELECT Name, Credits FROM Users", conn)

                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            labels.Add(reader("Name").ToString())
                            values.Add(Convert.ToInt32(reader("Credits")))
                        End While
                    End Using
                End Using

                Dim responseChart = JsonConvert.SerializeObject(New With {
                    .type = "chart",
                    .labels = labels,
                    .values = values
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(responseChart)

        End Select

    End Sub

End Class