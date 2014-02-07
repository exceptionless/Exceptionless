using System;
using System.Collections.Generic;
using System.Text;
using System.Web.UI.WebControls;
using System.Web.UI;


namespace Exceptionless.Client.Web
{
    [ToolboxData("<{0}:CaseDetail runat=\"server\"> </{0}:CaseDetail>")]
    public class ReportControl : CompositeControl
    {
        protected Label EmailAddressLabel;
        protected TextBox EmailAddressTextBox;
        protected Label DetailLabel;
        protected TextBox DetailTextBox;
        protected Button SendButton;

        public Guid MessageIdentifier
        {
            get
            {
                return new Guid(ViewState["MessageIdentifier"] as string);
            }
            set
            {
                ViewState["MessageIdentifier"] = value;
            }
        }

        protected override void CreateChildControls()
        {
            Controls.Clear();

            EmailAddressLabel = new Label();
            EmailAddressLabel.ID = "EmailAddressLabel";
            EmailAddressLabel.AssociatedControlID = "EmailAddressTextBox";
            EmailAddressLabel.Text = "Your email address (optional):";
            Controls.Add(EmailAddressLabel);

            EmailAddressTextBox = new TextBox();
            EmailAddressTextBox.ID = "EmailAddressTextBox";
            EmailAddressTextBox.Columns = 50;
            Controls.Add(EmailAddressTextBox);

            DetailLabel = new Label();
            DetailLabel.ID = "DetailLabel";
            DetailLabel.AssociatedControlID = "DetailTextBox";
            DetailLabel.Text = "Describe what you were doing when the error occurred (optional):";
            Controls.Add(DetailLabel);

            DetailTextBox = new TextBox();
            DetailTextBox.ID = "DetailTextBox";
            DetailTextBox.TextMode = TextBoxMode.MultiLine;
            DetailTextBox.Columns = 50;
            DetailTextBox.Rows = 10;
            Controls.Add(DetailTextBox);

            SendButton = new Button();
            SendButton.ID = "SendButton";
            SendButton.Text = "Send";
            SendButton.Click += OnSendButtonClick;
            Controls.Add(SendButton);

        }

        protected virtual void OnSendButtonClick(object sender, EventArgs e)
        {
            AddCaseDetailReport report = new AddCaseDetailReport(this.MessageIdentifier);
            report.Description = DetailTextBox.Text;
            report.EmailAddress = EmailAddressTextBox.Text;

            ExceptionlessManager.Current.AddCaseDetail(report);            
        }

        protected override void Render(HtmlTextWriter writer)
        {
            base.EnsureChildControls();

            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            EmailAddressLabel.RenderControl(writer);
            writer.RenderEndTag();

            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            EmailAddressTextBox.RenderControl(writer);
            writer.RenderEndTag();

            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            DetailLabel.RenderControl(writer);
            writer.RenderEndTag();

            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            DetailTextBox.RenderControl(writer);
            writer.RenderEndTag();

            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            SendButton.RenderControl(writer);
            writer.RenderEndTag();
        }
    }
}
