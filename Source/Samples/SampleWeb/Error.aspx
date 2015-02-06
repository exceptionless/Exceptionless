<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="Exceptionless" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<script runat="server">

    protected void SendButton_Click(object sender, EventArgs e) {
        string errorId = ExceptionlessClient.Default.GetLastReferenceId();
        string emailAddress = EmailAddressTextBox.Text;
        string description = DetailTextBox.Text;

        bool success = ExceptionlessClient.Default.UpdateUserEmailAndDescription(errorId, emailAddress, description);

        if (!success) {
            GenericPanel.Visible = true;
            AddDetailPanel.Visible = false;
            DetailSubmitedPanel.Visible = false;
        } else {
            GenericPanel.Visible = false;
            AddDetailPanel.Visible = false;
            DetailSubmitedPanel.Visible = true;
        }
    }

    protected void Page_Load(object sender, EventArgs e) {
        if (!IsPostBack) {
            if (String.IsNullOrEmpty(ExceptionlessClient.Default.GetLastReferenceId())) {
                GenericPanel.Visible = true;
                AddDetailPanel.Visible = false;
                DetailSubmitedPanel.Visible = false;
            }
        }
    }

</script>
<html xmlns="http://www.w3.org/1999/xhtml">
    <head runat="server">
        <title>Error</title>
        <style type="text/css">
            body {
                background-color: #FFF;
                font-family: verdana, arial, helvetica, sans-serif;
                text-align: center;
            }

            #content {
                margin-left: auto;
                margin-right: auto;
                margin-top: 60px;
                text-align: left;
                width: 800px;
            }

            #content h2 { color: red; }

            .lable { margin: 1px; }

            .input { margin-bottom: 8px; }
        </style>
    </head>
    <body>
        <form id="errorForm" runat="server">
            <div id="content">
                <asp:Panel ID="GenericPanel" runat="server" Visible="false">
                    <h2>
                        We are currently unable to serve your request</h2>
                    <p>
                        We apologize, but an error occurred and your request could not be completed.</p>
                </asp:Panel>
                <asp:Panel ID="AddDetailPanel" runat="server">
                    <h2>
                        We are currently unable to serve your request</h2>
                    <p>
                        We apologize, but an error occurred and your request could not be completed.</p>
                    <p>
                        This error has been logged. To help us diagnose the cause of this error and improve the software, please enter your email address, describe what you were doing when this error occurred, and send this report to us.</p>
                    <div class="label">
                        <asp:Label runat="server" Text="Your email address (optional):" ID="EmailAddressLabel" AssociatedControlID="EmailAddressTextBox" />
                    </div>
                    <div class="input">
                        <asp:TextBox runat="server" ID="EmailAddressTextBox" Columns="50"></asp:TextBox>
                    </div>
                    <div class="label">
                        <asp:Label runat="server" Text="Describe what you were doing when the error occurred (optional):" ID="DetailLabel" AssociatedControlID="DetailTextBox" />
                    </div>
                    <div class="input">
                        <asp:TextBox runat="server" ID="DetailTextBox" TextMode="MultiLine" Rows="10" Columns="60"></asp:TextBox>
                    </div>
                    <div>
                        <asp:Button runat="server" Text="Send" ID="SendButton" OnClick="SendButton_Click" />
                    </div>
                </asp:Panel>
                <asp:Panel ID="DetailSubmitedPanel" runat="server" Visible="false">
                    <p>
                        Thank You. Your details have been submitted.</p>
                </asp:Panel>
            </div>
        </form>
    </body>
</html>