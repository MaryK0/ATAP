FROM python:3.12-alpine AS docs
RUN pip install --no-cache-dir Markdown==3.7
WORKDIR /build
COPY README.md .
COPY web_lr4/render-docs.py .
RUN python render-docs.py

FROM nginx:1.28-alpine
COPY web_lr4/nginx.conf /etc/nginx/conf.d/default.conf
COPY web_lr4/static/ /usr/share/nginx/html/static/
COPY web_lr1/diagrams/screens/ /usr/share/nginx/html/static/img/
COPY web_lr4/management.html /usr/share/nginx/html/management.html
COPY --from=docs /build/documentation.html /usr/share/nginx/html/documentation.html
COPY web_lr1/diagrams/ /usr/share/nginx/html/documentation/assets/web_lr1/diagrams/
